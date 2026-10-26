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
    public TextBlock DetailValueNoteLabel => DetailValueNoteText;
    public TextBlock DetailAccessNoteLabel => DetailAccessNoteText;
    public TextBlock DetailDescriptionNoteLabel => DetailDescriptionNoteText;
    public TextBlock FormSummaryLabel => FormSummaryText;
    public TextBlock StagedSummaryLabel => StagedSummaryText;
    public TextBlock CompareSummaryLabel => CompareSummaryText;
    public TextBlock BulkSummaryLabel => BulkSummaryText;
    public TextBox BulkValueEditor => BulkValueBox;
    public ComboBox DetailAccessChoiceEditor => DetailAccessCombo;
    public Button ApplyEditsActionButton => ApplyEditsButton;
    public Button RevertFormActionButton => RevertFormButton;
    public Button DiscardDraftsActionButton => DiscardDraftsButton;
    public Button CompareActionButton => CompareButton;
    public Button BulkActionButton => BulkButton;
    public Button DiscardAllActionButton => DiscardAllButton;
    public Border DetailActionsPanel => DetailActionsCard;
    public Panel ParameterActionStrip => ParameterActionsPanel;
    public Button ReadParametersActionButton => ReadParametersButton;
    public Button ChooseFolderActionButton => ChooseFolderButton;
    public Button ReviewSaveActionButton => ReviewSaveButton;
    public Button BulkConfirmActionButton => BulkConfirmButton;
    public Border ExplorerStagedBadge => StagedBadge;
    public ListView ReviewList => ReviewListView;
    public TextBlock ReviewSummaryLabel => ReviewSummaryText;
    // The single review entry, in the top bar beside the staged-count badge. It is deliberately not
    // repeated next to a parameter: the review always covers the whole session, never only the selection.
    public Button ReviewOpenButton => TopReviewButton;
    public TextBlock StagedTrayLabel => StagedTrayText;

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

        // The view model owns the decision logic; this window only renders the choices.
        workspace.NavigationGuard = ShowUnsavedWorkPrompt;
    }

    /// <summary>Modal unsaved-work prompt; called on the UI thread from workspace commands.</summary>
    private NavigationDecision ShowUnsavedWorkPrompt(NavigationPrompt prompt)
    {
        UnsavedWorkWindow dialog = Owned(new UnsavedWorkWindow(prompt));
        dialog.ShowDialog();
        return dialog.Decision;
    }

    /// <summary>Shows the offline licence/notices view; re-uses one open window.</summary>
    private void ShowLicenses()
    {
        if (_licensesWindow is null)
        {
            _licensesWindow = Owned(new LicensesWindow());
            _licensesWindow.Closed += (_, _) => _licensesWindow = null;
            _licensesWindow.Show();
        }
        else
        {
            _licensesWindow.Activate();
        }
    }

    /// <summary>
    /// WPF rejects an Owner pointing at a window that has never been shown (tests, or a dialog
    /// requested during construction), so ownership is attached only once the shell is visible.
    /// </summary>
    private T Owned<T>(T dialog)
        where T : Window
    {
        if (IsLoaded)
        {
            dialog.Owner = this;
        }

        return dialog;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing must not silently drop staged work: the prompt decides, Cancel keeps the window open.
        if (!Workspace.ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
