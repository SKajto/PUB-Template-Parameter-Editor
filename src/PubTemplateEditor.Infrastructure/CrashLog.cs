using System.Text;

namespace PubTemplateEditor.Infrastructure;

/// <summary>
/// A small append-only record of faults that reached the application's top level, in the user's own
/// application-data folder next to the settings, backups and recovery journal.
///
/// An exception that ends up here is one no command handled, and on a machine the maintainer cannot sit
/// at, the log line is what a report consists of: without it the user has only "it closed". Writing the
/// log can never be the thing that fails, so every path through it is guarded and the failure of the log
/// is reported as null rather than as another exception.
/// </summary>
public sealed class CrashLog
{
    private const long RotateAtBytes = 512 * 1024;

    private readonly string _directory;

    /// <summary>Defaults to the user-scoped application data folder; tests pass a temporary one.</summary>
    public CrashLog(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory) ? SettingsService.AppDataDirectory : directory!;
    }

    public string FilePath => Path.Combine(_directory, "crash.log");

    /// <summary>The path written, or null when nothing could be written.</summary>
    public string? Write(string what, Exception? exception, string? context = null)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            Rotate();

            var text = new StringBuilder();
            text.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
                .Append("  ").Append(what).AppendLine();
            if (!string.IsNullOrWhiteSpace(context))
            {
                text.Append("  context   : ").Append(context).AppendLine();
            }

            if (exception is not null)
            {
                for (Exception? current = exception; current is not null; current = current.InnerException)
                {
                    text.Append("  exception : ").Append(current.GetType().FullName).AppendLine();
                    text.Append("  message   : ").Append(current.Message).AppendLine();
                    if (!string.IsNullOrWhiteSpace(current.StackTrace))
                    {
                        text.Append(current.StackTrace).AppendLine();
                    }
                }
            }

            File.AppendAllText(FilePath, text.AppendLine().ToString(), Encoding.UTF8);
            return FilePath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps one generation of history: a fault that repeats every launch would otherwise grow the file
    /// without bound, and the oldest records are the ones nobody was going to read anyway.
    /// </summary>
    private void Rotate()
    {
        if (File.Exists(FilePath) && new FileInfo(FilePath).Length > RotateAtBytes)
        {
            string archive = FilePath + ".1";
            File.Copy(FilePath, archive, overwrite: true);
            File.WriteAllText(FilePath, string.Empty);
        }
    }
}
