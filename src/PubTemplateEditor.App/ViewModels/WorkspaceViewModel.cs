using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// Main workspace state for S0/S1: remembered folder and theme, template explorer,
/// explicit Read parameters with per-file status. Editing/compare/save arrive in S2+.
/// </summary>
public sealed class WorkspaceViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly TemplateReaderService _reader = new();
    private EditorSettings _loadedSettings;
    private string? _filterText;
    private string? _statusMessage;
    private string? _folderPath;
    private bool _isBusy;
    private double _progress;
    private TemplateItem? _selectedItem;

    public WorkspaceViewModel(SettingsService settings, EditorSettings loadedSettings)
    {
        _settings = settings;
        _loadedSettings = loadedSettings;

        OpenFolderCommand = new RelayCommand(OpenFolder, () => !IsBusy);
        FocusFilterCommand = new RelayCommand(() => FilterFocusRequested?.Invoke());
        ReadParametersCommand = new AsyncRelayCommand(ReadParametersAsync, () => !IsBusy && FolderPath is not null);
        SelectAllForReadCommand = new RelayCommand(() => SetAllChecked(true), () => VisibleFiles.Count > 0);
        ClearReadSelectionCommand = new RelayCommand(() => SetAllChecked(false), () => VisibleFiles.Count > 0);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

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

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand FocusFilterCommand { get; }
    public AsyncRelayCommand ReadParametersCommand { get; }
    public RelayCommand SelectAllForReadCommand { get; }
    public RelayCommand ClearReadSelectionCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public string? FolderPath
    {
        get => _folderPath;
        private set => Set(ref _folderPath, value);
    }

    public string? FilterText
    {
        get => _filterText;
        set
        {
            if (Set(ref _filterText, value))
            {
                ApplyFilter();
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !_isBusy;

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public TemplateItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            // FILE-05: focusing a template never silently changes read/bulk selection.
            if (Set(ref _selectedItem, value))
            {
                Raise(nameof(ActiveTemplateInfo));
            }
        }
    }

    public string ActiveTemplateInfo =>
        SelectedItem is null ? "No template focused." : $"{SelectedItem.FileName} — {SelectedItem.Status}";

    public int SelectedReadCount => AllFiles.Count(f => f.IsSelectedForRead);

    public int TotalFileCount => AllFiles.Count;

    /// <summary>Set by the view so Ctrl+F can move keyboard focus to the filter box.</summary>
    public event Action? FilterFocusRequested;

    private void OpenFolder()
    {
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
            var items = Directory.EnumerateFiles(path)
                .Where(TemplateReaderService.IsTemplateFile)
                .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
                .Select(f => new TemplateItem(f, true))
                .ToList();

            AllFiles.Clear();
            foreach (var item in items)
            {
                AllFiles.Add(item);
            }

            FolderPath = path;
            ApplyFilter();
            Raise(nameof(SelectedReadCount));
            Raise(nameof(TotalFileCount));

            // FILE-04: reading is explicit; listing does not parse.
            StatusMessage = items.Count == 0
                ? "No .dta or template .xml files in this folder."
                : $"{items.Count} template file(s) listed. Press Read parameters to load declarations.";

            // Persist the working folder immediately (GEN/FILE-01).
            _loadedSettings.LastFolder = path;
            _settings.Save(_loadedSettings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Folder '{path}' could not be opened: {ex.Message}. Choose another folder.";
        }
    }

    private void ApplyFilter()
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
    }

    private void SetAllChecked(bool value)
    {
        foreach (var item in VisibleFiles)
        {
            item.IsSelectedForRead = value;
        }

        Raise(nameof(SelectedReadCount));
    }

    private async Task ReadParametersAsync(CancellationToken token)
    {
        var targets = AllFiles.Where(f => f.IsSelectedForRead).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "No templates selected for reading.";
            return;
        }

        IsBusy = true;
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
            Raise(nameof(SelectedReadCount));
        }
    }

    private void ToggleTheme()
    {
        var current = SettingsService.ParseTheme(_loadedSettings.Theme)
            ?? (((Application.Current.FindResource("WindowBackgroundBrush") as System.Windows.Media.SolidColorBrush)?.Color.R ?? 0) > 128
                ? ThemeChoice.Light
                : ThemeChoice.Dark);
        var next = current == ThemeChoice.Light ? ThemeChoice.Dark : ThemeChoice.Light;

        ThemeManager.Apply(Application.Current, next);
        _loadedSettings.Theme = next.ToString();
        _settings.Save(_loadedSettings);
        StatusMessage = $"{next} theme applied and remembered.";
    }
}
