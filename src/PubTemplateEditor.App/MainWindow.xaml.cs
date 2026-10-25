using System.Windows;
using System.Windows.Controls;
using PubTemplateEditor.App.ViewModels;

namespace PubTemplateEditor.App;

public partial class MainWindow : Window
{
    public WorkspaceViewModel Workspace { get; }

    /// <summary>Public handles to named parts (x:Name fields themselves are internal).</summary>
    public TextBox FilterControl => FilterBox;

    public TextBox SearchControl => ParameterSearchBox;

    public ListView ParameterTable => ParameterList;

    // Named text controls exposed for UI tests: reading the rendered value proves the
    // binding target resolves instead of silently showing nothing.
    public TextBlock BrandTitleLabel => BrandTitleText;
    public TextBlock FolderNameLabel => FolderNameText;
    public TextBlock FileCountBadge => FileCountText;
    public TextBlock SelectedCountLabel => SelectedCountText;
    public TextBlock ScopeLabel => ScopeText;
    public TextBlock ActiveTemplateLabel => ActiveTemplateText;
    public TextBlock SummaryLabel => SummaryText;
    public TextBlock FooterCountLabel => FooterCountText;
    public TextBlock FooterOperationLabel => FooterOperationText;
    public Button VersionLabel => VersionButton;
    public TextBlock DetailNameLabel => DetailNameText;
    public TextBlock DetailXmlTypeLabel => DetailXmlTypeText;
    public TextBox DetailValueEditor => DetailValueBox;
    public TextBox DetailAccessEditor => DetailAccessBox;
    public TextBox DetailDescriptionEditor => DetailDescriptionBox;
    public TextBlock ActionErrorLabel => ActionErrorText;

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
        workspace.ParameterSearchFocusRequested += () =>
        {
            ParameterSearchBox.Focus();
            ParameterSearchBox.SelectAll();
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
