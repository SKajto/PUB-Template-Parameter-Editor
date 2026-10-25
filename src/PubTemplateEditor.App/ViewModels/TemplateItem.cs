using PubTemplateEditor.App.Infrastructure;
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
    private int _stagedEditCount;
    private string _status = "Not read";
    private TemplateRecord? _record;
    private long _sizeBytes;
    private string? _listingError;

    public TemplateItem(string filePath, bool selectedForRead, long sizeBytes = 0, string? listingError = null)
    {
        FilePath = filePath;
        _isSelectedForRead = selectedForRead;
        _sizeBytes = sizeBytes;
        _listingError = listingError;
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

    public int StagedEditCount
    {
        get => _stagedEditCount;
        set
        {
            if (Set(ref _stagedEditCount, value))
            {
                Raise(nameof(StatusDisplay));
            }
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
            }
        }
    }

    public bool HasError => _status.StartsWith("Error", StringComparison.Ordinal) || _listingError is not null;

    public string StatusDisplay => _stagedEditCount > 0
        ? $"{_status} · {_stagedEditCount} staged"
        : _status;

    public TemplateRecord? Record
    {
        get => _record;
        set
        {
            _record = value;
            Raise(nameof(Record));
            Raise(nameof(IsLoaded));
            Raise(nameof(DeclarationCount));
        }
    }

    public bool IsLoaded => _record?.ReadState == TemplateReadState.Loaded;

    public int DeclarationCount => _record?.Declarations.Count ?? 0;

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
