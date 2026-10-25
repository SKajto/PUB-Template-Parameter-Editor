using System.Windows;
using System.Windows.Controls;
using PubTemplateEditor.App.ViewModels;

namespace PubTemplateEditor.App;

public partial class MainWindow : Window
{
    public WorkspaceViewModel Workspace { get; }

    /// <summary>Public handle to the named filter box (the x:Name field itself is internal).</summary>
    public TextBox FilterControl => FilterBox;

    private LicensesWindow? _licensesWindow;

    public MainWindow(WorkspaceViewModel workspace)
    {
        Workspace = workspace;
        DataContext = workspace;
        InitializeComponent();
        workspace.FilterFocusRequested += () =>
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
        };
        workspace.LicensesRequested += ShowLicenses;
    }

    /// <summary>Shows the offline licence/notices view; re-uses one open window.</summary>
    private void ShowLicenses()
    {
        if (_licensesWindow is null)
        {
            _licensesWindow = new LicensesWindow { Owner = this };
            _licensesWindow.Closed += (_, _) => _licensesWindow = null;
            _licensesWindow.Show();
        }
        else
        {
            _licensesWindow.Activate();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // S3+ will offer Save/Discard/Cancel when drafts or unapplied edits exist.
        base.OnClosing(e);
    }
}
