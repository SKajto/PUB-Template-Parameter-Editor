namespace PubTemplateEditor.Infrastructure;

/// <summary>
/// Directory paths in the form both the file system and the Windows shell accept.
///
/// A folder path reaches the application from several directions at once: a settings file that a person
/// may have edited, drag-and-drop from Explorer, a command line, or the folder dialog. The file system
/// tolerates '/' separators and device-namespace prefixes such as '\\?\'; the shell item API behind the
/// folder dialog does not - it answers E_INVALIDARG, which the dialog surfaces as an exception from
/// inside its own setup, before any window appears. Normalising once, here, keeps those two views of the
/// same path from diverging.
/// </summary>
public static class FolderPaths
{
    /// <summary>
    /// An absolute directory path with '\' separators and no trailing separator (a drive or UNC root
    /// keeps its one), or null when the text cannot name one. Never throws.
    /// </summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string candidate = path.Trim().Trim('"').Trim();
        if (candidate.Length == 0 || !Path.IsPathRooted(candidate))
        {
            return null;                        // a path relative to some current directory is not a remembered folder
        }

        if (candidate.StartsWith(@"\\?\", StringComparison.Ordinal)
            || candidate.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return null;                        // device and shell-namespace forms are not paths the shell item API takes
        }

        try
        {
            string full = Path.GetFullPath(candidate);
            string? root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            return full.Length == root.Length
                ? full                                       // "C:\" or "\\server\share\" stays as it is
                : full.TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException
                                   or IOException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the path may be handed to the shell as the folder dialog's starting point. It has to be a
    /// normalised path that exists on this machine right now; anything else costs the user the dialog,
    /// so the caller opens it without a starting folder instead.
    /// </summary>
    public static bool IsShellAddressable(string? path)
    {
        if (string.IsNullOrEmpty(path) || path!.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            return false;
        }

        if (Normalize(path) != path)
        {
            return false;
        }

        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
