namespace PubTemplateEditor.Infrastructure;

/// <summary>
/// The decision taken when a fault reaches the top level of the application: record it, tell the user in
/// words they can act on, and say whether the program may carry on.
///
/// Letting a fault end the process is what turned one unusable remembered folder into a window vanishing
/// with no message on a machine nobody could sit at - staged edits gone, nothing written down. Letting
/// every fault be swallowed instead would keep a broken program running silently, so the decision is
/// bounded: a fixed number of faults are reported and survived, and beyond that the application is
/// allowed to stop.
///
/// Deliberately free of any UI type: the caller decides how a fault is shown, which keeps this decision
/// testable on any machine.
/// </summary>
public sealed class UiFaultGuard
{
    /// <summary>How many faults are reported and survived before the application is allowed to stop.</summary>
    public const int RecoveryLimit = 3;

    private readonly CrashLog _log;
    private int _faults;

    public UiFaultGuard(CrashLog log) => _log = log;

    /// <summary>How many faults this session has already recorded.</summary>
    public int FaultCount => _faults;

    /// <summary>
    /// Records one fault and returns whether the caller may keep running. The report callback is the
    /// caller's way of showing something; a report that itself fails must not turn into a second fault.
    /// </summary>
    public bool Handle(string what, Exception? exception, Action<string?, bool>? report = null, string? context = null)
    {
        string? logPath = _log.Write(what, exception, context);
        bool keepRunning = Interlocked.Increment(ref _faults) <= RecoveryLimit;

        try
        {
            report?.Invoke(logPath, keepRunning);
        }
        catch (Exception)
        {
            // A report that cannot be delivered changes nothing about the decision that was made.
        }

        return keepRunning;
    }
}
