using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Core.Search;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>Access column filter of the parameter table.</summary>
public enum AccessFilterChoice
{
    All,
    Internal,
    External,
}

/// <summary>
/// Workspace state for the S2 read-only explorer: remembered folder/theme, template
/// explorer with an explicit read scope, Read parameters with per-file status, and the
/// active template's parameter table with search, access filter and detail panel.
/// Editing, compare and save arrive in later milestones and are absent here by design.
/// </summary>
public sealed partial class WorkspaceViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly TemplateReaderService _reader = new();
    private readonly TemplateFolderScanner _scanner = new();

    /// <summary>
    /// Staged field changes of this session. Nothing in loading, comparing, bulk staging or
    /// editing touches a file; only the save review (S4) writes, and only for selected fields.
    /// </summary>
    private readonly DraftStore Drafts = new();

    private EditorSettings _loadedSettings;

    private string? _filterText;
    private string? _parameterQuery;
    private string? _statusMessage;
    private string? _actionError;
    private string? _actionNotice;
    private string? _folderPath;
    private bool _isBusy;
    private double _progress;
    private AccessFilterChoice _accessFilter = AccessFilterChoice.All;
    private TemplateItem? _activeFile;
    private ParameterRowItem? _selectedParameter;
    private ThemeChoice _theme;

    /// <param name="saveStateDirectory">
    /// Where backups and the recovery journal of the save step live. Null means the user-scoped
    /// application data folder, which is what the shipped app uses; tests pass a temporary folder so a
    /// save test cannot touch real user state.
    /// </param>
    public WorkspaceViewModel(SettingsService settings, EditorSettings loadedSettings, string? saveStateDirectory = null)
    {
        _settings = settings;
        _saver = new TemplateSaveService(saveStateDirectory);
        _loadedSettings = loadedSettings;
        _theme = SettingsService.ParseTheme(loadedSettings.Theme) ?? ThemeChoice.System;

        OpenFolderCommand = new RelayCommand(OpenFolder, () => !IsBusy);
        FocusFilterCommand = new RelayCommand(() => FilterFocusRequested?.Invoke());
        FocusParameterSearchCommand = new RelayCommand(() => ParameterSearchFocusRequested?.Invoke());
        ReadParametersCommand = new AsyncRelayCommand(ReadParametersAsync, () => !IsBusy && FolderPath is not null);
        SelectVisibleCommand = new RelayCommand(() => SetAllChecked(true), () => VisibleFiles.Count > 0);
        ClearVisibleCommand = new RelayCommand(() => SetAllChecked(false), () => VisibleFiles.Count > 0);
        SelectActiveFileCommand = new RelayCommand(item => SelectActiveFile(item as TemplateItem));
        SetThemeCommand = new RelayCommand(value => ApplyTheme(ParseThemeChoice(value)));
        DismissNoticeCommand = new RelayCommand(() => ActionNotice = null);
        InitEditingCommands();
        InitReviewCommands();
        InitReviewCommands();
        // Licences must stay reachable offline from inside the app (GPL distribution rule).
        // The view decides how to present them; the view model stays free of window types.
        OpenLicensesCommand = new RelayCommand(() => LicensesRequested?.Invoke());

        RefreshThemeSelection();

        // FILE-01: restore remembered folder; a missing folder produces a clear message.
        if (!string.IsNullOrWhiteSpace(loadedSettings.LastFolder))
        {
            TrySetFolder(loadedSettings.LastFolder!);
        }
        else
        {
            StatusMessage = "Choose a template folder to begin.";
        }
    }

    public ObservableCollection<TemplateItem> AllFiles { get; } = [];
    public ObservableCollection<TemplateItem> VisibleFiles { get; } = [];
    public ObservableCollection<ParameterRowItem> AllParameters { get; } = [];
    public ObservableCollection<ParameterRowItem> VisibleParameters { get; } = [];

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand FocusFilterCommand { get; }
    public RelayCommand FocusParameterSearchCommand { get; }
    public AsyncRelayCommand ReadParametersCommand { get; }
    public RelayCommand SelectVisibleCommand { get; }
    public RelayCommand ClearVisibleCommand { get; }
    public RelayCommand SelectActiveFileCommand { get; }
    public RelayCommand SetThemeCommand { get; }
    public RelayCommand DismissNoticeCommand { get; }
    public RelayCommand OpenLicensesCommand { get; }

    public string ProductName => ProductInfo.Name;

    public string VersionLabel => $"Version {VersionText}";

    public string VersionText => ProductInfo.Version;

    public string? FolderPath
    {
        get => _folderPath;
        private set
        {
            if (Set(ref _folderPath, value))
            {
                Raise(nameof(FolderName));
                Raise(nameof(HasFolder));
                Raise(nameof(FolderSummaryText));
            }
        }
    }

    public string FolderName => _folderPath is null ? "No folder selected" : Path.GetFileName(_folderPath.TrimEnd('\\', '/'));

    public bool HasFolder => _folderPath is not null;

    /// <summary>Filename filter of the explorer; filtering never changes the read selection.</summary>
    public string? FilterText
    {
        get => _filterText;
        set
        {
            if (Set(ref _filterText, value))
            {
                ApplyFileFilter();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (Set(ref _statusMessage, value))
            {
                Raise(nameof(IsStatusEmpty));
            }
        }
    }

    public bool IsStatusEmpty => string.IsNullOrEmpty(_statusMessage);

    /// <summary>Recoverable failure of a workspace action, shown as a dismissible notice.</summary>
    public string? ActionError
    {
        get => _actionError;
        private set
        {
            if (Set(ref _actionError, value))
            {
                Raise(nameof(HasActionError));
            }
        }
    }

    public bool HasActionError => !string.IsNullOrEmpty(_actionError);

    /// <summary>Success confirmation with an explicit dismiss control.</summary>
    public string? ActionNotice
    {
        get => _actionNotice;
        private set
        {
            if (Set(ref _actionNotice, value))
            {
                Raise(nameof(HasActionNotice));
            }
        }
    }

    public bool HasActionNotice => !string.IsNullOrEmpty(_actionNotice);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(IsNotBusy));
                Raise(nameof(IsIdle));
                Raise(nameof(ProgressText));
            }
        }
    }

    public bool IsNotBusy => !_isBusy;

    /// <summary>True while nothing is running and no folder is loaded: the welcome state.</summary>
    public bool IsIdle => !IsBusy && _folderPath is null;

    public double Progress
    {
        get => _progress;
        private set
        {
            if (Set(ref _progress, value))
            {
                Raise(nameof(ProgressText));
            }
        }
    }

    public string ProgressText => _isBusy ? $"Reading… {Progress:P0}" : string.Empty;

    public string FolderSummaryText => _folderPath is null
        ? "No folder selected"
        : $"{AllFiles.Count} template(s) in folder";

    public string OperationText => _isBusy
        ? ProgressText
        : "Offline · Review Save writes selected changes";

    public ThemeChoice Theme
    {
        get => _theme;
        private set
        {
            if (Set(ref _theme, value))
            {
                RefreshThemeSelection();
            }
        }
    }

    public bool IsLightSelected => _theme == ThemeChoice.Light;
    public bool IsDarkSelected => _theme == ThemeChoice.Dark;
    public bool IsSystemSelected => _theme == ThemeChoice.System;

    // ---------------------------------------------------------------- file scope

    public int SelectedReadCount => AllFiles.Count(f => f.IsSelectedForRead);

    public int TotalFileCount => AllFiles.Count;

    public bool HasFiles => AllFiles.Count > 0;

    public bool HasVisibleFiles => VisibleFiles.Count > 0;

    /// <summary>Folder has entries but the filename filter hides them all.</summary>
    public bool ShowNoFileMatches => AllFiles.Count > 0 && VisibleFiles.Count == 0;

    public string ScopeDescription => SelectedReadCount > 0
        ? $"Reading {SelectedReadCount} explicitly selected template{(SelectedReadCount == 1 ? "" : "s")}."
        : $"No read selection is active; reading all {VisibleFiles.Count} currently visible template{(VisibleFiles.Count == 1 ? "" : "s")}.";

    /// <summary>FILE-05: the focused template, independent from read/bulk selection.</summary>
    public TemplateItem? ActiveFile
    {
        get => _activeFile;
        private set
        {
            if (Set(ref _activeFile, value))
            {
                if (value is not null)
                {
                    foreach (var item in AllFiles)
                    {
                        item.IsActive = ReferenceEquals(item, value);
                    }
                }

                Raise(nameof(ActiveTemplateName));
                Raise(nameof(HasActiveFile));
                Raise(nameof(ActiveParameterSummary));
                Raise(nameof(ShowEmptyNoActiveFile));
                LoadParameters();
            }
        }
    }

    public string ActiveTemplateName => _activeFile?.FileName ?? "Parameter browser";

    public bool HasActiveFile => _activeFile is not null;

    public void SelectActiveFile(TemplateItem? item)
    {
        if (item is null || ReferenceEquals(item, _activeFile))
        {
            return;
        }

        if (!ConfirmNavigation(NavigationReason.ChangeTemplate))
        {
            return;
        }

        ActiveFile = item;
        ParameterQuery = null;
        SelectedAccessFilter = AccessFilterChoice.All;
    }

    // ------------------------------------------------------------ parameter view

    /// <summary>Name/description search over the active template's declarations.</summary>
    public string? ParameterQuery
    {
        get => _parameterQuery;
        set
        {
            if (Set(ref _parameterQuery, value))
            {
                ApplyParameterFilter();
            }
        }
    }

    public AccessFilterChoice SelectedAccessFilter
    {
        get => _accessFilter;
        set
        {
            if (Set(ref _accessFilter, value))
            {
                ApplyParameterFilter();
            }
        }
    }

    /// <summary>ComboBox items in AccessFilterChoice order, with UI labels.</summary>
    public string[] AccessFilterLabels { get; } = ["All access", "Internal", "External"];

    public int AccessFilterIndex
    {
        get => (int)SelectedAccessFilter;
        set
        {
            if (Enum.IsDefined(typeof(AccessFilterChoice), value))
            {
                SelectedAccessFilter = (AccessFilterChoice)value;
            }
        }
    }

    public int TotalParameterCount => AllParameters.Count;

    public int MatchedParameterCount => VisibleParameters.Count;

    public bool HasParameters => AllParameters.Count > 0;

    public bool HasVisibleParameters => VisibleParameters.Count > 0;

    /// <summary>The three mutually exclusive empty states of the parameter area.</summary>
    public bool ShowEmptyNoActiveFile => ActiveFile is null && !IsBusy;

    public bool ShowEmptyNotRead => ActiveFile is not null && AllParameters.Count == 0;

    public bool ShowEmptyNoMatches => AllParameters.Count > 0 && VisibleParameters.Count == 0;

    public string ActiveParameterSummary => _activeFile is null
        ? "No active template"
        : _activeFile.Record?.ReadState == TemplateReadState.Loaded
            ? $"{MatchedParameterCount} matched of {TotalParameterCount} parameters"
            : "Parameters not read yet";

    public ParameterRowItem? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (ReferenceEquals(value, _selectedParameter))
            {
                return;
            }

            // SWITCH-01: switching away from a form with unapplied changes needs an explicit decision.
            // When the user stays, the setter raises the old value so a bound list rewinds its selection.
            if (!ConfirmNavigation(NavigationReason.ChangeParameter))
            {
                Raise(nameof(SelectedParameter));
                return;
            }

            if (Set(ref _selectedParameter, value))
            {
                Raise(nameof(HasSelectedParameter));
                LoadEditorForm();
                RefreshReviewAndActionStates();
            }
        }
    }

    public bool HasSelectedParameter => _selectedParameter is not null;

    /// <summary>Why unsupported declaration shapes stay read-only, shown in the detail panel.</summary>
    public string DetailReadOnlyReason => _selectedParameter is { HasReadOnlyShape: true } p ? p.ReadOnlyShapeReasons : string.Empty;

    public bool HasDetailReadOnlyReason => DetailReadOnlyReason.Length > 0;

    /// <summary>Set by the view so Ctrl+F can move keyboard focus to the filter box.</summary>
    public event Action? FilterFocusRequested;

    /// <summary>Set by the view so Ctrl+E can move keyboard focus to the parameter search box.</summary>
    public event Action? ParameterSearchFocusRequested;

    /// <summary>Raised when the user asks for the offline licence/notices view.</summary>
    public event Action? LicensesRequested;

    // ------------------------------------------------------------------- actions

    private void OpenFolder()
    {
        if (!ConfirmNavigation(NavigationReason.ChangeTemplate))
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose the folder containing .dta templates",
            Multiselect = false,
        };

        if (FolderPath is not null && Directory.Exists(FolderPath))
        {
            dialog.InitialDirectory = FolderPath;
        }

        if (dialog.ShowDialog() == true)
        {
            TrySetFolder(dialog.FolderName);
        }
    }

    private void TrySetFolder(string path)
    {
        try
        {
            var listed = _scanner.Scan(path);

            AllFiles.Clear();
            foreach (var entry in listed)
            {
                AllFiles.Add(new TemplateItem(entry.FilePath, true, entry.SizeBytes, entry.ListingError, Drafts));
            }

            FolderPath = path;
            ActiveFile = AllFiles.FirstOrDefault();
            ApplyFileFilter();
            RefreshCounters();

            // FILE-04: reading is explicit; listing does not parse.
            StatusMessage = AllFiles.Count == 0
                ? "No .dta template files in this folder."
                : $"{AllFiles.Count} template file(s) listed. Press Read parameters to load declarations.";
            ActionNotice = null;
            ActionError = null;

            // Persist the working folder immediately (GEN/FILE-01).
            _loadedSettings.LastFolder = path;
            _settings.Save(_loadedSettings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Folder '{path}' could not be opened: {ex.Message}. Choose another folder.";
        }
    }

    private void ApplyFileFilter()
    {
        VisibleFiles.Clear();
        foreach (var item in AllFiles)
        {
            if (string.IsNullOrEmpty(_filterText) ||
                item.FileName.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase))
            {
                VisibleFiles.Add(item);
            }
        }

        Raise(nameof(HasVisibleFiles));
        Raise(nameof(ShowNoFileMatches));
        Raise(nameof(ScopeDescription));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetAllChecked(bool value)
    {
        foreach (var item in VisibleFiles)
        {
            item.IsSelectedForRead = value;
        }

        RefreshCounters();
    }

    private async Task ReadParametersAsync(CancellationToken token)
    {
        // Re-reading replaces the in-session records, so staged work would silently disappear.
        if (!ConfirmNavigation(NavigationReason.ReReadTemplates))
        {
            return;
        }

        // FILE-05: an empty checkbox set falls back to the visible scope, never to "nothing".
        var targets = AllFiles.Where(f => f.IsSelectedForRead && f.IsListable).ToList();
        if (targets.Count == 0)
        {
            ActionError = "No readable templates are selected or visible for reading.";
            return;
        }

        IsBusy = true;
        ActionError = null;
        ActionNotice = null;
        Progress = 0;
        int done = 0;
        int errors = 0;

        try
        {
            foreach (var item in targets)
            {
                token.ThrowIfCancellationRequested();
                TemplateRecord record = await Task.Run(() => _reader.Load(item.FilePath, token), token);

                item.Record = record;
                item.Status = record.ReadState switch
                {
                    TemplateReadState.Loaded => $"{record.Declarations.Count} parameter(s)",
                    _ => $"Error: {record.ReadError}",
                };
                if (record.ReadState == TemplateReadState.Error)
                {
                    errors++;
                }

                done++;
                Progress = (double)done / targets.Count;
                if (ReferenceEquals(item, ActiveFile))
                {
                    LoadParameters();
                }

                RefreshCounters();
            }

            StatusMessage = errors == 0
                ? $"Read {done} template(s), {AllFiles.Sum(f => f.Record?.Declarations.Count ?? 0)} declaration(s) total."
                : $"Read {done} template(s); {errors} had errors and stay visible with their status.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Reading cancelled after {done} of {targets.Count} file(s).";
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
            RefreshCounters();
            Raise(nameof(ShowEmptyNotRead));
        }
    }

    /// <summary>Rebuilds the parameter table of the focused template from its record.</summary>
    private void LoadParameters()
    {
        var previous = _selectedParameter;
        AllParameters.Clear();
        VisibleParameters.Clear();
        _selectedParameter = null;
        Raise(nameof(SelectedParameter));

        var record = _activeFile?.Record;
        if (record is { ReadState: TemplateReadState.Loaded })
        {
            foreach (var declaration in record.Declarations)
            {
                AllParameters.Add(new ParameterRowItem(declaration, _activeFile!, Drafts));
            }
        }

        // A re-read of the same declaration keeps the user's row selection.
        if (previous is not null)
        {
            var same = AllParameters.FirstOrDefault(r => r.DeclarationId == previous.DeclarationId);
            if (same is not null)
            {
                _selectedParameter = same;
                Raise(nameof(SelectedParameter));
            }
        }

        ApplyParameterFilter();
        Raise(nameof(HasSelectedParameter));
        Raise(nameof(HasParameters));
        Raise(nameof(TotalParameterCount));
        LoadEditorForm();
        RefreshReviewAndActionStates();
    }

    private void ApplyParameterFilter()
    {
        VisibleParameters.Clear();
        foreach (var row in AllParameters)
        {
            bool matchesAccess = SelectedAccessFilter switch
            {
                AccessFilterChoice.Internal => string.Equals(row.AccessLabel, "internal", StringComparison.OrdinalIgnoreCase),
                AccessFilterChoice.External => string.Equals(row.AccessLabel, "external", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };

            if (!matchesAccess)
            {
                continue;
            }

            if (ParameterSearchMatcher.Matches(row.Declaration.ExactName, row.Declaration.Description.LogicalValue, _parameterQuery ?? string.Empty))
            {
                VisibleParameters.Add(row);
            }
        }

        Raise(nameof(MatchedParameterCount));
        Raise(nameof(ActiveParameterSummary));
        Raise(nameof(HasVisibleParameters));
        Raise(nameof(ShowEmptyNoActiveFile));
        Raise(nameof(ShowEmptyNotRead));
        Raise(nameof(ShowEmptyNoMatches));
    }

    private void RefreshCounters()
    {
        Raise(nameof(SelectedReadCount));
        Raise(nameof(TotalFileCount));
        Raise(nameof(HasFiles));
        Raise(nameof(ScopeDescription));
        Raise(nameof(FolderSummaryText));
        Raise(nameof(ActiveParameterSummary));
    }

    private void ApplyTheme(ThemeChoice choice)
    {
        if (choice == Theme)
        {
            return;
        }

        ThemeManager.Apply(Application.Current, choice);
        Theme = choice;
        _loadedSettings.Theme = choice.ToString();
        _settings.Save(_loadedSettings);
    }

    private void RefreshThemeSelection()
    {
        Raise(nameof(IsLightSelected));
        Raise(nameof(IsDarkSelected));
        Raise(nameof(IsSystemSelected));
    }

    private static ThemeChoice ParseThemeChoice(object? value) => value switch
    {
        ThemeChoice choice => choice,
        string text when SettingsService.ParseTheme(text) is ThemeChoice parsed => parsed,
        _ => ThemeChoice.System,
    };
}
