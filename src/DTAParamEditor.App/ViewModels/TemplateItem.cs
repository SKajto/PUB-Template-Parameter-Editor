using System.Collections.ObjectModel;
using DTAParamEditor.App.Infrastructure;
using DTAParamEditor.Core.Model;
using DTAParamEditor.Infrastructure;

namespace DTAParamEditor.App.ViewModels;

/// <summary>One template file row in the explorer.</summary>
public sealed class TemplateItem : ViewModelBase
{
    public string FilePath { get; }

    private bool _isSelectedForRead;
    private bool _isDraft;
    private string _status = "Not read";
    private TemplateRecord? _record;

    public TemplateItem(string filePath, bool selectedForRead)
    {
        FilePath = filePath;
        _isSelectedForRead = selectedForRead;
    }

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public bool IsSelectedForRead
    {
        get => _isSelectedForRead;
        set => Set(ref _isSelectedForRead, value);
    }

    public bool IsDraft
    {
        get => _isDraft;
        set
        {
            if (Set(ref _isDraft, value))
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

    public bool HasError => _status.StartsWith("Error", StringComparison.Ordinal);

    public string StatusDisplay => _isDraft ? $"{_status} • draft" : _status;

    public TemplateRecord? Record
    {
        get => _record;
        set
        {
            _record = value;
            Raise(nameof(Record));
        }
    }
}
