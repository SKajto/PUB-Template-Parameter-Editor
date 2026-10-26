using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// One template file row in the explorer. FILE-05: the read-selection checkbox and the
/// focused ("active") template are independent; focusing a row never changes read scope.
/// </summary>
public sealed class TemplateItem : ViewModelBase
{
    public string FilePath { get; }

    private bool _isSelectedForRead;
    private bool _isActive;
    private string _status = "Not read";
    private TemplateRecord? _record;
    private long _sizeBytes;
    private string? _listingError;
    private readonly DraftStore? _drafts;

    public TemplateItem(string filePath, bool selectedForRead, long sizeBytes = 0, string? listingError = null, DraftStore? drafts = null)
    {
        FilePath = filePath;
        _isSelectedForRead = selectedForRead;
        _sizeBytes = sizeBytes;
        _listingError = listingError;
        _drafts = drafts;
        _status = listingError is null ? "Not read" : "Not readable";
    }

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public bool IsSelectedForRead
    {
        get => _isSelectedForRead;
        set => Set(ref _isSelectedForRead, value);
    }

    /// <summary>FILE-05: focus/active state, separate from read and bulk selection.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (Set(ref _isActive, value))
            {
                Raise(nameof(StatusDisplay));
            }
        }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (Set(ref _sizeBytes, value))
            {
                Raise(nameof(SizeText));
            }
        }
    }

    public string SizeText => FormatSize(_sizeBytes);

    public string? ListingError
    {
        get => _listingError;
        set
        {
            if (Set(ref _listingError, value))
            {
                Raise(nameof(IsListable));
            }
        }
    }

    /// <summary>A file the folder listing could not stat is disabled for read selection.</summary>
    public bool IsListable => _listingError is null;

    /// <summary>
    /// Staged (not yet saved) field changes belonging to this file. Read from the session draft
    /// store, so a template keeps its dirty marker while another template is displayed.
    /// </summary>
    public int StagedEditCount => _drafts?.CountForFile(FilePath) ?? 0;

    /// <summary>True when this template holds staged edits that no save has written yet.</summary>
    public bool IsDraft => StagedEditCount > 0;

    /// <summary>Re-raises the dirty marker after staging or discarding drafts.</summary>
    public void RefreshDraftState()
    {
        Raise(nameof(StagedEditCount));
        Raise(nameof(IsDraft));
        Raise(nameof(StatusDisplay));
        Raise(nameof(StagedChangesText));
        Raise(nameof(RowTooltip));
    }

    /// <summary>
    /// Hover text for the file list: which file this is, how many parameters it holds, and how many
    /// staged changes it carries. Listing does not parse, so an unread file says so instead of claiming
    /// zero parameters, which would read as an empty template.
    /// </summary>
    public string RowTooltip
    {
        get
        {
            var lines = new List<string> { FileName };
            lines.Add(IsLoaded
                ? $"{DeclarationCount} parameter(s)"
                : HasError
                    ? $"not readable: {Status}"
                    : "not read yet");

            if (StagedEditCount > 0)
            {
                lines.Add($"{StagedEditCount} staged change(s), not saved");
            }

            lines.Add($"{SizeText} · {Status}");
            lines.Add(FilePath);
            return string.Join(Environment.NewLine, lines);
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                Raise(nameof(StatusDisplay));
                Raise(nameof(HasError));
                Raise(nameof(RowTooltip));
            }
        }
    }

    public bool HasError => _status.StartsWith("Error", StringComparison.Ordinal) || _listingError is not null;

    public string StatusDisplay => StagedEditCount > 0
        ? $"{Status} · {StagedEditCount} staged"
        : Status;

    public TemplateRecord? Record
    {
        get => _record;
        set
        {
            _record = value;
            Raise(nameof(Record));
            Raise(nameof(IsLoaded));
            Raise(nameof(DeclarationCount));
            Raise(nameof(ParameterCountText));
            Raise(nameof(RowTooltip));
        }
    }

    public bool IsLoaded => _record?.ReadState == TemplateReadState.Loaded;

    public int DeclarationCount => _record?.Declarations.Count ?? 0;

    /// <summary>
    /// How many parameters this file holds, said honestly: listing does not parse, so a file that has
    /// never been read is not "0 parameters", it is unread.
    /// </summary>
    public string ParameterCountText => IsLoaded
        ? $"{DeclarationCount} parameter(s)"
        : HasError
            ? "not readable"
            : "not read yet";

    /// <summary>Staged changes of this file, as the hover card words it.</summary>
    public string StagedChangesText => StagedEditCount > 0
        ? $"{StagedEditCount} staged change(s), not saved"
        : "none staged";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }
}
