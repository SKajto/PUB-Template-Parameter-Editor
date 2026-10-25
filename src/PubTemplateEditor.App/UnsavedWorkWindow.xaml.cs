using System.Windows;
using System.Windows.Controls;
using PubTemplateEditor.App.ViewModels;

namespace PubTemplateEditor.App;

/// <summary>
/// Modal prompt for navigation that would throw away work (SWITCH-01). It offers the safe order of
/// actions: apply the form first, discard only the unapplied typing, discard everything of the
/// session, or stay. Closing the window with Esc is treated as "keep editing".
/// </summary>
public partial class UnsavedWorkWindow : Window
{
    public NavigationDecision Decision { get; private set; } = NavigationDecision.KeepEditing;

    /// <summary>Named handles for UI tests.</summary>
    public NavigationPrompt Prompt { get; }
    public Button ApplyChoice => ApplyButton;
    public Button DiscardFormChoice => DiscardFormButton;
    public Button DiscardAllChoice => DiscardAllButton;
    public Button KeepEditingChoice => KeepEditingButton;
    public TextBlock MessageLabel => MessageText;
    public TextBlock HeadlineLabel => HeadlineText;

    public UnsavedWorkWindow(NavigationPrompt prompt)
    {
        InitializeComponent();
        Prompt = prompt;

        MessageText.Text = Describe(prompt);
        DiscardAllButton.Visibility = prompt.StagedChangeCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyButton.IsEnabled = prompt.UnappliedFieldCount > 0;

        ApplyButton.Click += (_, _) => Choose(NavigationDecision.ApplyForms);
        DiscardFormButton.Click += (_, _) => Choose(NavigationDecision.DiscardForms);
        DiscardAllButton.Click += (_, _) => Choose(NavigationDecision.DiscardFormsAndStaged);
        KeepEditingButton.Click += (_, _) => Choose(NavigationDecision.KeepEditing);
    }

    private void Choose(NavigationDecision decision)
    {
        Decision = decision;
        Close();
    }

    private static string Describe(NavigationPrompt prompt)
    {
        string what = prompt.Reason switch
        {
            NavigationReason.ChangeParameter => "You are switching to another parameter.",
            NavigationReason.ChangeTemplate => "You are switching to another template or folder.",
            NavigationReason.ReReadTemplates => "Reading again replaces the templates held in this session.",
            NavigationReason.DiscardStagedChanges => "You are discarding the staged changes of this session.",
            NavigationReason.CloseWorkspace => "You are closing the editor.",
            _ => "You are leaving the current form.",
        };

        var parts = new List<string> { what };
        if (prompt.UnappliedFieldCount > 0)
        {
            parts.Add($"{prompt.UnappliedFieldCount} field change(s) typed in the details form have not been staged yet.");
        }

        if (prompt.StagedChangeCount > 0)
        {
            parts.Add($"{prompt.StagedChangeCount} staged change(s) exist in this session; they were not written to any file.");
        }

        parts.Add("Nothing is written to a template by any of these choices.");
        return string.Join(" ", parts);
    }
}
