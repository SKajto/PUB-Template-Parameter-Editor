using System.Windows;
using DTAParamEditor.App.ViewModels;

namespace DTAParamEditor.App;

public partial class MainWindow : Window
{
    public WorkspaceViewModel Workspace { get; }

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
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // S3+ will offer Save/Discard/Cancel when drafts or unapplied edits exist.
        base.OnClosing(e);
    }
}
