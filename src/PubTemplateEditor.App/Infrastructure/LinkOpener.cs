using System.ComponentModel;
using System.Diagnostics;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Opens a web address in whatever the user's machine has for it. Behind an interface for two reasons:
/// a test must not launch a browser, and an address that this machine cannot open has to be reported
/// in the window rather than end a click.
/// </summary>
public interface ILinkOpener
{
    void Open(string url);
}

/// <summary>
/// Hands the address to the shell, so the user's own browser choice and policy apply - the application
/// contains no browser and fetches nothing itself.
/// </summary>
public sealed class ShellLinkOpener : ILinkOpener
{
    public void Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("That address is not a web page link.");
        }

        try
        {
            Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"This Windows account has no program to open {parsed.AbsoluteUri} with. The address is shown there in full, so it can be entered by hand.",
                ex);
        }
    }
}
