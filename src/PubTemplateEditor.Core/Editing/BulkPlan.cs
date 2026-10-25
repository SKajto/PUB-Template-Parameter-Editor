using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Editing;

public enum BulkTargetStatus
{
    /// <summary>Exactly one declaration with this exact name and an editable default value.</summary>
    Eligible,

    /// <summary>The template declares no parameter with this exact name.</summary>
    Missing,

    /// <summary>The name is declared more than once, so the target is ambiguous and must stay untouched.</summary>
    Ambiguous,

    /// <summary>The declaration exists but its default value has no verified source span.</summary>
    ReadOnlyShape,

    /// <summary>The template was not read successfully in this session.</summary>
    NotLoaded,
}

/// <summary>
/// One candidate for a bulk value change, with the reason when it is not eligible
/// (REQUIREMENTS BULK-02/03: ambiguous or unsupported targets are explained, never silently skipped).
/// </summary>
public sealed record BulkTarget(
    string FilePath,
    string FileName,
    BulkTargetStatus Status,
    string? Reason,
    int DeclarationId,
    string? CurrentValue,
    bool CurrentHasDraft)
{
    public bool IsSelectable => Status == BulkTargetStatus.Eligible;
}

/// <summary>
/// Plans bulk targets for a single exact parameter name. Bulk editing changes default values only:
/// target access and description are never part of a bulk proposal, which is enforced here rather
/// than left to the caller.
/// </summary>
public static class BulkPlan
{
    public static IReadOnlyList<BulkTarget> Plan(
        string? declarationName,
        IReadOnlyList<TemplateRecord> records,
        DraftStore drafts,
        IReadOnlySet<string>? scope = null,
        FieldKind field = FieldKind.DefaultValue)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(drafts);

        if (field != FieldKind.DefaultValue)
        {
            throw new ArgumentException(
                "Bulk editing stages default values only; access and description are per-declaration edits.",
                nameof(field));
        }

        if (string.IsNullOrEmpty(declarationName))
        {
            return [];
        }

        var targets = new List<BulkTarget>();
        foreach (TemplateRecord record in records)
        {
            if (scope is not null && !scope.Contains(record.FilePath))
            {
                continue;
            }

            targets.Add(PlanOne(record, declarationName!, drafts));
        }

        return targets;
    }

    /// <summary>
    /// Stages the value for the selected targets only. A target that the plan did not classify as
    /// <see cref="BulkTargetStatus.Eligible"/> is refused here as well, so a UI that forgot to
    /// filter can never write an ambiguous or unsupported declaration.
    /// </summary>
    public static IReadOnlyList<(BulkTarget Target, StageResult Result)> StageValues(
        string? declarationName,
        string proposedValue,
        IEnumerable<BulkTarget> selectedTargets,
        IReadOnlyList<TemplateRecord> records,
        DraftStore drafts)
    {
        ArgumentNullException.ThrowIfNull(selectedTargets);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(proposedValue);

        if (string.IsNullOrEmpty(declarationName))
        {
            return [];
        }

        var outcomes = new List<(BulkTarget, StageResult)>();
        foreach (BulkTarget target in selectedTargets)
        {
            if (target.Status != BulkTargetStatus.Eligible)
            {
                outcomes.Add((target, new StageResult(FieldKind.DefaultValue, StageStatus.Rejected,
                    $"{target.FileName} is not an eligible bulk target: {target.Reason}")));
                continue;
            }

            TemplateRecord? record = records.FirstOrDefault(
                r => string.Equals(r.FilePath, target.FilePath, StringComparison.Ordinal));
            VariableDeclaration? declaration = record?.FindDeclaration(target.DeclarationId);
            if (record is null || declaration is null)
            {
                outcomes.Add((target, new StageResult(FieldKind.DefaultValue, StageStatus.Rejected,
                    $"{target.FileName} no longer holds this declaration in the session.")));
                continue;
            }

            outcomes.Add((target, drafts.Stage(record.FilePath, declaration, FieldKind.DefaultValue, proposedValue)));
        }

        return outcomes;
    }

    private static BulkTarget PlanOne(TemplateRecord record, string name, DraftStore drafts)
    {
        if (record.ReadState != TemplateReadState.Loaded)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.NotLoaded,
                record.ReadState == TemplateReadState.Error
                    ? $"Cannot be a bulk target: {record.ReadError}"
                    : "Cannot be a bulk target: this template has not been read in this session.",
                DeclarationId: -1, CurrentValue: null, CurrentHasDraft: false);
        }

        var matches = record.Declarations
            .Where(d => string.Equals(d.ExactName, name, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Missing,
                $"'{name}' is not declared in this template.", DeclarationId: -1,
                CurrentValue: null, CurrentHasDraft: false);
        }

        if (matches.Count > 1)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Ambiguous,
                $"'{name}' is declared {matches.Count} times here; writing would be a guess, so this target stays untouched.",
                DeclarationId: -1, CurrentValue: null, CurrentHasDraft: false);
        }

        VariableDeclaration target = matches[0];
        DeclarationField defaultValue = target.DefaultValue;

        if (!defaultValue.Present || defaultValue.Capability == FieldCapability.Missing)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.ReadOnlyShape,
                defaultValue.CapabilityReason
                ?? $"'{name}' has no default property in this template; bulk editing cannot add one.",
                target.DeclarationId, CurrentValue: null, CurrentHasDraft: false);
        }

        if (defaultValue.Capability == FieldCapability.ReadOnlyUnsupported)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.ReadOnlyShape,
                defaultValue.CapabilityReason
                ?? $"'{name}' holds an unobserved default shape that is preserved read-only.",
                target.DeclarationId, defaultValue.LogicalValue, CurrentHasDraft: false);
        }

        bool hasDraft = drafts.HasChange(record.FilePath, target.DeclarationId, FieldKind.DefaultValue);
        return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Eligible, Reason: null,
            target.DeclarationId,
            drafts.EffectiveValue(record.FilePath, target, FieldKind.DefaultValue),
            hasDraft);
    }
}
