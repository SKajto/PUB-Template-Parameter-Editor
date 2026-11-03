using System;
using System.Collections.Generic;
using System.IO;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The bounded decision behind the top-level fault handling: a fault is written down, and the program
/// keeps running for a limited number of them. Without the record, a user on another machine has only
/// "it closed" to report; without the limit, a fault that repeats would ask about it forever.
/// </summary>
public class UiFaultGuardTests
{
    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-faultguard-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void AFaultIsRecordedBeforeAnyoneIsAskedAboutIt()
    {
        string directory = TempDir("record");
        try
        {
            var guard = new UiFaultGuard(new CrashLog(directory));
            var seen = new List<bool>();

            bool keepRunning = guard.Handle(
                "unhandled UI exception",
                new ArgumentException("Value does not fall within the expected range."),
                (logPath, running) => { seen.Add(running); Assert.NotNull(logPath); },
                "action=choose folder");

            Assert.True(keepRunning);
            Assert.Equal(new[] { true }, seen);
            Assert.Contains("Value does not fall within the expected range.",
                            File.ReadAllText(Path.Combine(directory, "crash.log")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FaultsAreSurvivedUpToALimitAndThenNotAllowedToContinue()
    {
        string directory = TempDir("limit");
        try
        {
            var guard = new UiFaultGuard(new CrashLog(directory));
            var decisions = new List<bool>();
            for (int attempt = 0; attempt <= UiFaultGuard.RecoveryLimit; attempt++)
            {
                decisions.Add(guard.Handle("repeating fault", new InvalidOperationException("again"), null));
            }

            // A fault the user can act on is survived; the same fault arriving again and again is a
            // broken program, and asking about it forever is not a service to anybody.
            Assert.Equal(new[] { true, true, true, false }, decisions);
            Assert.Equal(UiFaultGuard.RecoveryLimit, decisions.Count(d => d));
            Assert.Equal(UiFaultGuard.RecoveryLimit + 1, guard.FaultCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EveryFaultIsInTheLogEvenWhenNobodyIsAsked()
    {
        string directory = TempDir("nobody");
        try
        {
            var guard = new UiFaultGuard(new CrashLog(directory));
            guard.Handle("fatal unhandled exception", new Exception("background thread"));
            guard.Handle("fatal unhandled exception", new Exception("another one"));

            string text = File.ReadAllText(Path.Combine(directory, "crash.log"));
            Assert.Contains("background thread", text, StringComparison.Ordinal);
            Assert.Contains("another one", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AReportThatItselfFailsDoesNotBecomeAnotherFault()
    {
        string directory = TempDir("reporting");
        try
        {
            var guard = new UiFaultGuard(new CrashLog(directory));
            bool keepRunning = guard.Handle("fault", new ArgumentException("bad folder"),
                                            (_, _) => throw new InvalidOperationException("no window to show this in"));

            Assert.True(keepRunning);
            Assert.Contains("bad folder", File.ReadAllText(Path.Combine(directory, "crash.log")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheDecisionSurvivesAnUnwritableLog()
    {
        string directory = TempDir("unwritable-log");
        string blocker = Path.Combine(directory, "a-file");
        File.WriteAllText(blocker, "a file where the log folder should be");
        try
        {
            var guard = new UiFaultGuard(new CrashLog(Path.Combine(blocker, "nested")));
            string? observedLogPath = "unset";
            bool keepRunning = guard.Handle("fault", new Exception("still decided"),
                                            (logPath, _) => observedLogPath = logPath);

            Assert.True(keepRunning);
            Assert.Null(observedLogPath);                  // the caller is told there is no log to point at
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
