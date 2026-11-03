using System;
using System.IO;
using System.Text;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The record of a fault that reached the top level of the application. On a machine the maintainer
/// cannot sit at, this file is the report, so it has to contain the fault and must never be the thing
/// that fails.
/// </summary>
public class CrashLogTests
{
    private static string TempDir(string kind)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pubte-crashlog-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void AFaultIsWrittenWithItsTypeMessageAndStack()
    {
        string directory = TempDir("write");
        try
        {
            string? written = new CrashLog(directory).Write("unhandled UI exception",
                                                            new ArgumentException("Value does not fall within the expected range."));

            Assert.NotNull(written);
            string text = File.ReadAllText(written!);
            Assert.Contains("unhandled UI exception", text, StringComparison.Ordinal);
            Assert.Contains("System.ArgumentException", text, StringComparison.Ordinal);
            Assert.Contains("Value does not fall within the expected range.", text, StringComparison.Ordinal);
            Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InnerFaultsAndTheContextOfTheFailureAreRecorded()
    {
        string directory = TempDir("chain");
        try
        {
            var outer = new InvalidOperationException("the folder dialog could not be opened",
                          new ArgumentException("Value does not fall within the expected range."));
            string? written = new CrashLog(directory).Write("dialog", outer, "folder=C:\\templates");

            string text = File.ReadAllText(written!);
            Assert.Contains("folder=C:\\templates", text, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException", text, StringComparison.Ordinal);
            Assert.Contains("System.ArgumentException", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FaultsAccumulateRatherThanOverwriteEachOther()
    {
        string directory = TempDir("append");
        try
        {
            var log = new CrashLog(directory);
            log.Write("first", new ArgumentException("one"));
            long afterFirst = new FileInfo(log.FilePath).Length;
            log.Write("second", new InvalidOperationException("two"));

            string text = File.ReadAllText(log.FilePath);
            Assert.Contains("first", text, StringComparison.Ordinal);
            Assert.Contains("second", text, StringComparison.Ordinal);
            Assert.Contains("System.ArgumentException", text, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException", text, StringComparison.Ordinal);
            Assert.True(new FileInfo(log.FilePath).Length > afterFirst,
                        "a later fault must not replace an earlier one: the order of the records is the evidence");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ASecondGenerationIsKeptWhenTheLogGrows()
    {
        string directory = TempDir("rotate");
        try
        {
            var log = new CrashLog(directory);
            File.WriteAllText(log.FilePath, new string('x', 600 * 1024));

            log.Write("after rotation", new ArgumentException("visible"));

            Assert.True(File.Exists(log.FilePath + ".1"), "the old records should survive as one generation");
            string text = File.ReadAllText(log.FilePath);
            Assert.Contains("after rotation", text, StringComparison.Ordinal);
            Assert.DoesNotContain("xxxx", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReportingAFaultNeverBecomesTheFault()
    {
        string directory = TempDir("unwritable");
        string blocker = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blocker, "a file where a folder would be");
        try
        {
            string? written = new CrashLog(Path.Combine(blocker, "sub")).Write("nowhere", new Exception("cannot be written"));
            Assert.Null(written);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AFaultWithoutAnExceptionObjectIsStillRecorded()
    {
        string directory = TempDir("noexception");
        try
        {
            string? written = new CrashLog(directory).Write("background thread died", null, "isTerminating=True");
            string text = File.ReadAllText(written!);
            Assert.Contains("background thread died", text, StringComparison.Ordinal);
            Assert.Contains("isTerminating=True", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
