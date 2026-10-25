using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Editing;

public enum StageStatus
{
    /// <summary>A proposed value different from the baseline is now staged in memory.</summary>
    Staged,

    /// <summary>The proposal equals the stored baseline, so no draft is needed and any older draft was removed.</summary>
    ClearedAsNoOp,

    /// <summary>The field cannot be edited; nothing changed and the reason explains why.</summary>
    Rejected,
}

public sealed record StageResult(FieldKind Field, StageStatus Status, string? Reason)
{
    public bool IsChange => Status == StageStatus.Staged;
}

/// <summary>
/// In-memory staged edits for one working session (REQUIREMENTS EDIT-01/EDIT-02).
///
/// Staging never writes to disk and never mutates the loaded <see cref="VariableDeclaration"/>:
/// the baseline stays exactly as read, and every proposal is addressed by
/// <see cref="ChangeKey"/> (file + declaration + field). A proposal that equals the baseline is
/// dropped instead of being kept as a "change", so counts, review and saving all see real deltas.
/// </summary>
public sealed class DraftStore
{
    private readonly Dictionary<ChangeKey, DraftChange> _drafts = [];

    public IEnumerable<DraftChange> Changes => _drafts.Values;

    public int Count => _drafts.Count;

    public bool HasAny => _drafts.Count > 0;

    public DraftChange? Find(string filePath, int declarationId, FieldKind field) =>
        _drafts.TryGetValue(new ChangeKey(filePath, declarationId, field), out DraftChange? change)
            ? change
            : null;

    public bool HasChange(string filePath, int declarationId, FieldKind field) =>
        _drafts.ContainsKey(new ChangeKey(filePath, declarationId, field));

    public bool HasChangesForDeclaration(string filePath, int declarationId) =>
        _drafts.Keys.Any(k => k.FilePath == filePath && k.DeclarationId == declarationId);

    public int CountForDeclaration(string filePath, int declarationId) =>
        _drafts.Keys.Count(k => k.FilePath == filePath && k.DeclarationId == declarationId);

    public IEnumerable<DraftChange> ForFile(string filePath) =>
        _drafts.Where(kv => kv.Key.FilePath == filePath).Select(kv => kv.Value);

    public int CountForFile(string filePath) => _drafts.Keys.Count(k => k.FilePath == filePath);

    public IEnumerable<DraftChange> ForField(FieldKind field) =>
        _drafts.Where(kv => kv.Key.Field == field).Select(kv => kv.Value);

    /// <summary>Number of staged changes per file; used by list badges and the review summary.</summary>
    public IReadOnlyDictionary<string, int> CountByFile() =>
        _drafts.Keys.GroupBy(k => k.FilePath).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Value the UI shows and comparison uses: the staged proposal when one exists,
    /// otherwise the untouched baseline. Empty and missing stay distinguishable
    /// ("" versus null).
    /// </summary>
    public string? EffectiveValue(string filePath, VariableDeclaration declaration, FieldKind field)
    {
        DraftChange? change = Find(filePath, declaration.DeclarationId, field);
        return change?.ProposedValue ?? declaration.GetField(field).LogicalValue;
    }

    public bool IsFieldDirty(string filePath, VariableDeclaration declaration, FieldKind field) =>
        Find(filePath, declaration.DeclarationId, field) is not null;

    public int DirtyFieldCount(string filePath, VariableDeclaration declaration) =>
        TemplateFieldRules.StagesFields.Count(field => IsFieldDirty(filePath, declaration, field));

    /// <summary>
    /// Stages one field. Never throws for user-hostile input: unsupported shapes, read-only names
    /// and invalid access vocabulary come back as <see cref="StageStatus.Rejected"/> with the reason
    /// the UI shows next to the field.
    /// </summary>
    public StageResult Stage(string filePath, VariableDeclaration declaration, FieldKind field, string? proposedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(declaration);

        string label = TemplateFieldRules.DisplayName(field);

        if (field == FieldKind.Name)
        {
            return new StageResult(field, StageStatus.Rejected,
                "Parameter names are read-only in this release; renaming would break every reference to the parameter.");
        }

        DeclarationField current = declaration.GetField(field);

        if (!current.Present || current.Capability == FieldCapability.Missing)
        {
            return new StageResult(field, StageStatus.Rejected,
                current.CapabilityReason
                ?? $"The {label} field is missing in this declaration; it cannot be edited.");
        }

        if (current.Capability == FieldCapability.ReadOnlyUnsupported)
        {
            return new StageResult(field, StageStatus.Rejected,
                current.CapabilityReason
                ?? $"The {label} field has no verified source span and is kept read-only.");
        }

        if (field == FieldKind.Access && !TemplateFieldRules.IsSupportedAccess(proposedValue))
        {
            return new StageResult(field, StageStatus.Rejected,
                $"Access must be exactly one of {TemplateFieldRules.AccessVocabulary}; '{proposedValue}' is not an observed value.");
        }

        ChangeKey key = new(filePath, declaration.DeclarationId, field);
        string? baseline = current.LogicalValue;

        // No-op normalisation: proposing the stored value again is not a change.
        if (string.Equals(baseline, proposedValue, StringComparison.Ordinal))
        {
            if (_drafts.Remove(key))
            {
                return new StageResult(field, StageStatus.ClearedAsNoOp,
                    $"{label} is back to the stored value; the staged change was removed.");
            }

            return new StageResult(field, StageStatus.ClearedAsNoOp, null);
        }

        _drafts[key] = new DraftChange(
            key,
            baseline,
            proposedValue,
            ProposedFromDraft: true)
        {
            DeclarationName = declaration.DisplayName,
        };

        return new StageResult(field, StageStatus.Staged, null);
    }

    /// <summary>
    /// Stages the fields present in <paramref name="formValues"/>. Fields the user did not touch are
    /// simply reported as unchanged, so a partially edited form never overwrites other fields.
    /// </summary>
    public IReadOnlyList<StageResult> StageDeclaration(
        string filePath,
        VariableDeclaration declaration,
        IReadOnlyDictionary<FieldKind, string?> formValues)
    {
        ArgumentNullException.ThrowIfNull(formValues);

        var results = new List<StageResult>(formValues.Count);
        foreach (FieldKind field in TemplateFieldRules.StagesFields)
        {
            if (formValues.TryGetValue(field, out string? value))
            {
                results.Add(Stage(filePath, declaration, field, value));
            }
        }

        return results;
    }

    public int DiscardForFile(string filePath)
    {
        var keys = _drafts.Keys.Where(k => k.FilePath == filePath).ToList();
        foreach (ChangeKey key in keys)
        {
            _drafts.Remove(key);
        }

        return keys.Count;
    }

    public int DiscardForDeclaration(string filePath, int declarationId)
    {
        var keys = _drafts.Keys.Where(k => k.FilePath == filePath && k.DeclarationId == declarationId).ToList();
        foreach (ChangeKey key in keys)
        {
            _drafts.Remove(key);
        }

        return keys.Count;
    }

    public void Clear() => _drafts.Clear();
}
