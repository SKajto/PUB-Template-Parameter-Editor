using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Editing;

/// <summary>
/// Compares one declaration across the templates loaded in this session
/// (REQUIREMENTS CMP-01..CMP-04). Matching is by exact declared name, never by
/// case-insensitive or trimmed text, and both sides use the working value, i.e. a staged
/// in-memory proposal is visible in the comparison.
/// </summary>
public static class ParameterComparator
{
    /// <summary>
    /// <paramref name="scope"/> limits the compared files; null means "all readable templates in
    /// the session". The active template is always present as the reference column.
    /// </summary>
    public static IReadOnlyList<TemplateComparison> Compare(
        string activeFilePath,
        VariableDeclaration activeDeclaration,
        IReadOnlyList<TemplateRecord> records,
        DraftStore drafts,
        IReadOnlySet<string>? scope = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(activeFilePath);
        ArgumentNullException.ThrowIfNull(activeDeclaration);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(drafts);

        string? activeName = activeDeclaration.ExactName;

        var rows = new List<TemplateComparison>
        {
            Self(activeFilePath, activeDeclaration, drafts),
        };

        foreach (TemplateRecord record in records)
        {
            if (string.Equals(record.FilePath, activeFilePath, StringComparison.Ordinal))
            {
                continue;
            }

            if (scope is not null && !scope.Contains(record.FilePath))
            {
                continue;
            }

            rows.Add(CompareOne(activeFilePath, activeDeclaration, activeName, record, drafts));
        }

        return rows;
    }

    private static TemplateComparison Self(
        string activeFilePath,
        VariableDeclaration active,
        DraftStore drafts)
    {
        var differences = new List<FieldDiff>();
        foreach (FieldKind field in TemplateFieldRules.StagesFields)
        {
            string? value = drafts.EffectiveValue(activeFilePath, active, field);
            differences.Add(new FieldDiff(
                field,
                FieldCompareState.Equal,
                value,
                value,
                ActiveHasDraft: drafts.HasChange(activeFilePath, active.DeclarationId, field),
                TargetHasDraft: false,
                Lines: []));
        }

        return new TemplateComparison(
            activeFilePath,
            Path.GetFileName(activeFilePath),
            CompareState.Self,
            drafts.HasChangesForDeclaration(activeFilePath, active.DeclarationId),
            "Reference template (the focused one in the table)",
            differences)
        {
            DeclarationId = active.DeclarationId,
        };
    }

    private static TemplateComparison CompareOne(
        string activeFilePath,
        VariableDeclaration active,
        string? activeName,
        TemplateRecord record,
        DraftStore drafts)
    {
        if (record.ReadState != TemplateReadState.Loaded)
        {
            return new TemplateComparison(
                record.FilePath,
                record.FileName,
                CompareState.NotLoaded,
                HasUnsavedDrafts: drafts.CountForFile(record.FilePath) > 0,
                Reason: record.ReadState == TemplateReadState.Error
                    ? $"Not compared: {record.ReadError}"
                    : "Not compared: this template has not been read in this session.",
                Differences: []);
        }

        if (string.IsNullOrEmpty(activeName))
        {
            return new TemplateComparison(
                record.FilePath,
                record.FileName,
                CompareState.Ineligible,
                HasUnsavedDrafts: drafts.CountForFile(record.FilePath) > 0,
                Reason: "The focused declaration has no usable name, so nothing can be matched.",
                Differences: []);
        }

        var matches = record.Declarations
            .Where(d => string.Equals(d.ExactName, activeName, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
        {
            return new TemplateComparison(
                record.FilePath,
                record.FileName,
                CompareState.Missing,
                HasUnsavedDrafts: drafts.CountForFile(record.FilePath) > 0,
                Reason: $"No declaration named '{activeName}' in this template.",
                Differences: []);
        }

        if (matches.Count > 1)
        {
            return new TemplateComparison(
                record.FilePath,
                record.FileName,
                CompareState.Ineligible,
                HasUnsavedDrafts: drafts.CountForFile(record.FilePath) > 0,
                Reason: $"'{activeName}' is declared {matches.Count} times here; the match is ambiguous, so nothing is compared or written.",
                Differences: []);
        }

        VariableDeclaration target = matches[0];
        var differences = new List<FieldDiff>();
        foreach (FieldKind field in TemplateFieldRules.StagesFields)
        {
            differences.Add(DiffField(activeFilePath, active, record, target, field, drafts));
        }

        bool anyDifference = differences.Any(d => d.State != FieldCompareState.Equal);
        return new TemplateComparison(
            record.FilePath,
            record.FileName,
            anyDifference ? CompareState.Different : CompareState.Same,
            drafts.HasChangesForDeclaration(record.FilePath, target.DeclarationId),
            Reason: null,
            differences)
        {
            DeclarationId = target.DeclarationId,
        };
    }

    private static FieldDiff DiffField(
        string activeFilePath,
        VariableDeclaration active,
        TemplateRecord record,
        VariableDeclaration target,
        FieldKind field,
        DraftStore drafts)
    {
        DeclarationField activeField = active.GetField(field);
        DeclarationField targetField = target.GetField(field);

        string? activeValue = drafts.EffectiveValue(activeFilePath, active, field);
        string? targetValue = drafts.EffectiveValue(record.FilePath, target, field);

        bool activeHasDraft = drafts.HasChange(activeFilePath, active.DeclarationId, field);
        bool targetHasDraft = drafts.HasChange(record.FilePath, target.DeclarationId, field);

        FieldCompareState state;
        if (!targetField.Present || targetField.Capability == FieldCapability.Missing)
        {
            // "not declared at all" and "declared but empty" are different facts.
            state = FieldCompareState.MissingField;
        }
        else if (!string.Equals(activeValue, targetValue, StringComparison.Ordinal))
        {
            state = FieldCompareState.Different;
        }
        else if (string.IsNullOrEmpty(targetValue))
        {
            state = FieldCompareState.EmptyValue;
        }
        else
        {
            state = FieldCompareState.Equal;
        }

        IReadOnlyList<DiffLine> lines = state == FieldCompareState.Equal
            ? []
            : LineDiff.Diff(activeValue, targetValue)
                .Select(pair => new DiffLine(pair.Changed, pair.NewText ?? pair.OldText ?? string.Empty))
                .ToList();

        return new FieldDiff(field, state, activeValue, targetValue, activeHasDraft, targetHasDraft, lines);
    }
}
