namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// The seam through which the workspace asks the user for a folder. Behind an interface because the
/// interesting part is not showing the dialog - it is what happens when the shell refuses it, which
/// needs a desktop session to trigger and a test double to verify.
/// </summary>
public interface IFolderPicker
{
    /// <summary>
    /// The chosen folder, or null when the user cancelled. Throws when the shell refuses the dialog
    /// itself; the caller decides what that means for the user.
    /// </summary>
    /// <param name="initialDirectory">
    /// A starting folder already established as usable, or null to let the dialog open at its own
    /// default location.
    /// </param>
    string? PickFolder(string? initialDirectory, string title);
}

/// <summary>
/// The Windows folder dialog (the shell's common item dialog in folder mode). It deliberately catches
/// nothing: a cancelled dialog is a normal null result, while a shell refusal is an exception the caller
/// reports and recovers from.
/// </summary>
public sealed class ShellFolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialDirectory, string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrEmpty(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
