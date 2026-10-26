using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    public ItemsControl ReviewList => ReviewGroupsList;
    public TextBlock ReviewSummaryLabel => ReviewSummaryText;
    // The single review entry, in the top bar beside the staged-count badge. It is deliberately not
    // repeated next to a parameter: the review always covers the whole session, never only the selection.
    public Button ReviewOpenButton => TopReviewButton;
    public TextBlock StagedTrayLabel => StagedTrayText;

    /// <summary>The template list: rows are selected as a whole, no option button involved.</summary>
    public ListBox TemplateRows => TemplateList;

    public Border TemplatesPanel => TemplatesPanelBorder;

    public ComboBox RecentFoldersControl => RecentFoldersBox;

    public Border DropHintPanel => DropHint;

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

        RestoreColumnWidths();
    }

    // ------------------------------------------------------- parameter table columns

    /// <summary>
    /// Applies the widths the user dragged the columns to last time. Column resizing is per column, so the
    /// saved list is read by position; a stored list of a different shape is simply ignored.
    /// </summary>
    private void RestoreColumnWidths()
    {
        if (ParameterList.View is not GridView view)
        {
            return;
        }

        IReadOnlyList<double> saved = Workspace.SavedParameterColumnWidths;
        for (int i = 0; i < view.Columns.Count && i < saved.Count; i++)
        {
            view.Columns[i].Width = saved[i];
        }
    }

    private void SaveColumnWidths()
    {
        if (ParameterList.View is not GridView view)
        {
            return;
        }

        Workspace.SaveParameterColumnWidths(view.Columns.Select(c => c.Width).ToList());
    }

    // --------------------------------------------------------------- recent folders

    private bool _recentSelectionGuard;

    private void RecentFoldersBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_recentSelectionGuard)
        {
            return;
        }

        var box = (ComboBox)sender;
        string? path = box.SelectedItem as string;

        // Reset before opening: the same folder has to be selectable twice in a row, and a cancelled
        // navigation must not leave the combo showing a folder that was never listed.
        _recentSelectionGuard = true;
        box.SelectedItem = null;
        _recentSelectionGuard = false;

        if (path is not null)
        {
            Workspace.OpenRecentFolderCommand.Execute(path);
        }
    }

    // --------------------------------------------------------------- drag and drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        string? path = FirstDroppedPath(e.Data);
        bool accepts = IsOpenablePath(path);
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        DropHint.Visibility = accepts ? Visibility.Visible : Visibility.Collapsed;
        DropHintText.Text = accepts
            ? $"Drop to open '{FileNameOf(path!)}' and list its templates"
            : string.Empty;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        string? path = FirstDroppedPath(e.Data);
        if (IsOpenablePath(path))
        {
            Workspace.TryOpenDroppedPath(path);
        }
    }

    private static bool IsOpenablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return Directory.Exists(path)
                || (File.Exists(path) && string.Equals(Path.GetExtension(path), ".dta", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string FileNameOf(string path)
    {
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    private static string? FirstDroppedPath(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            return files[0];
        }

        if (data.GetData(DataFormats.UnicodeText) is string text && text.Trim().Length > 0)
        {
            return text.Trim();
        }

        return null;
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

        SaveColumnWidths();
        base.OnClosing(e);
    }
}
