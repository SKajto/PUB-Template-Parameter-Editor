using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Editing;

public enum BulkTargetStatus
{
    /// <summary>
    /// Exactly one declaration with this exact name. Whether a given field can be written there is
    /// decided per field, in <see cref="BulkTarget.Fields"/>: a template may accept the value and
    /// still have no description property to write into.
    /// </summary>
    Eligible,

    /// <summary>The template declares no parameter with this exact name.</summary>
    Missing,

    /// <summary>The name is declared more than once, so the target is ambiguous and must stay untouched.</summary>
    Ambiguous,

    /// <summary>The template was not read successfully in this session.</summary>
    NotLoaded,
}

/// <summary>
/// One field of one bulk target: whether this template can receive that field, the current value,
/// and why not when it cannot. Explaining the refusal is part of the feature - a change that
/// silently skips a template is how an editor loses people's work.
/// </summary>
public sealed record BulkFieldTarget(
    FieldKind Field,
    bool IsWritable,
    string? Reason,
    string? CurrentValue,
    bool CurrentHasDraft)
{
    public string Label => TemplateFieldRules.DisplayName(Field);
}

/// <summary>
/// One candidate template for a bulk change of a single exact parameter name
/// (REQUIREMENTS BULK-02/03: ambiguous or unsupported targets are explained, never silently skipped).
/// </summary>
public sealed record BulkTarget(
    string FilePath,
    string FileName,
    BulkTargetStatus Status,
    string? Reason,
    int DeclarationId,
    IReadOnlyList<BulkFieldTarget> Fields)
{
    /// <summary>The name resolves to exactly one declaration here.</summary>
    public bool IsSelectable => Status == BulkTargetStatus.Eligible;

    /// <summary>Convenience for callers that display the value column.</summary>
    public string? CurrentValue => Field(FieldKind.DefaultValue)?.CurrentValue;

    public BulkFieldTarget? Field(FieldKind field) => Fields.FirstOrDefault(f => f.Field == field);

    public bool CanWrite(FieldKind field) => IsSelectable && Field(field)?.IsWritable == true;

    public IEnumerable<BulkFieldTarget> WritableFields => Fields.Where(f => f.IsWritable);

    public IEnumerable<BulkFieldTarget> BlockedFields => Fields.Where(f => !f.IsWritable);
}

/// <summary>
/// Plans and stages bulk changes for one exact parameter name across templates. Bulk editing copies
/// the fields the user selected - default value, access (internal/external) and description - onto
/// the same-named declaration of each chosen template. Parameter names are never bulk-written, and a
/// field a target cannot hold is refused with a reason instead of quietly skipped.
/// </summary>
public static class BulkPlan
{
    /// <summary>The fields bulk editing offers, in the order the UI shows them.</summary>
    public static readonly FieldKind[] BulkFields =
    [
        FieldKind.DefaultValue,
        FieldKind.Access,
        FieldKind.Description,
    ];

    public static IReadOnlyList<BulkTarget> Plan(
        string? declarationName,
        IReadOnlyList<TemplateRecord> records,
        DraftStore drafts,
        IReadOnlySet<string>? scope = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(drafts);

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
    /// Stages each requested field on each selected target. A target the plan did not classify as
    /// <see cref="BulkTargetStatus.Eligible"/>, and a field the plan marked unwritable, are refused
    /// here too - a UI that forgot to filter can never write an ambiguous or unsupported declaration.
    /// </summary>
    public static IReadOnlyList<(BulkTarget Target, FieldKind Field, StageResult Result)> Stage(
        string? declarationName,
        IReadOnlyDictionary<FieldKind, string> proposedByField,
        IEnumerable<BulkTarget> selectedTargets,
        IReadOnlyList<TemplateRecord> records,
        DraftStore drafts)
    {
        ArgumentNullException.ThrowIfNull(selectedTargets);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(proposedByField);

        if (string.IsNullOrEmpty(declarationName))
        {
            return [];
        }

        var fields = BulkFields.Where(proposedByField.ContainsKey).ToList();
        if (fields.Count == 0)
        {
            throw new ArgumentException(
                "Bulk editing needs at least one field to apply; choose a value, the access or the description.",
                nameof(proposedByField));
        }

        var outcomes = new List<(BulkTarget, FieldKind, StageResult)>();

        foreach (BulkTarget target in selectedTargets)
        {
            if (!target.IsSelectable)
            {
                foreach (FieldKind field in fields)
                {
                    outcomes.Add((target, field, new StageResult(field, StageStatus.Rejected,
                        $"{target.FileName}: {target.Reason ?? "this template is not a bulk target"}")));
                }

                continue;
            }

            TemplateRecord? record = records.FirstOrDefault(
                r => string.Equals(r.FilePath, target.FilePath, StringComparison.Ordinal));
            VariableDeclaration? declaration = record?.FindDeclaration(target.DeclarationId);

            if (record is null || declaration is null)
            {
                foreach (FieldKind field in fields)
                {
                    outcomes.Add((target, field, new StageResult(field, StageStatus.Rejected,
                        $"{target.FileName} no longer holds this declaration in the session.")));
                }

                continue;
            }

            foreach (FieldKind field in fields)
            {
                BulkFieldTarget? plan = target.Field(field);
                if (plan is null || !plan.IsWritable)
                {
                    outcomes.Add((target, field, new StageResult(field, StageStatus.Rejected,
                        $"{target.FileName}: {plan?.Reason ?? $"this template cannot receive the {TemplateFieldRules.DisplayName(field)}"}")));
                    continue;
                }

                string proposed = proposedByField[field];

                // The access vocabulary is small and fixed by what native templates actually use;
                // anything else would write a value this editor has never seen preserved correctly.
                if (field == FieldKind.Access && !TemplateFieldRules.IsSupportedAccess(proposed))
                {
                    outcomes.Add((target, field, new StageResult(field, StageStatus.Rejected,
                        $"{target.FileName}: access must be one of {TemplateFieldRules.AccessVocabulary}, not \"{proposed}\".")));
                    continue;
                }

                outcomes.Add((target, field, drafts.Stage(record.FilePath, declaration, field, proposed)));
            }
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
                DeclarationId: -1, Fields: []);
        }

        var matches = record.Declarations
            .Where(d => string.Equals(d.ExactName, name, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Missing,
                $"'{name}' is not declared in this template.", DeclarationId: -1, Fields: []);
        }

        if (matches.Count > 1)
        {
            return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Ambiguous,
                $"'{name}' is declared {matches.Count} times here; writing would be a guess, so this target stays untouched.",
                DeclarationId: -1, Fields: []);
        }

        VariableDeclaration target = matches[0];

        return new BulkTarget(record.FilePath, record.FileName, BulkTargetStatus.Eligible, Reason: null,
            target.DeclarationId,
            [
                FieldOf(record, target, FieldKind.DefaultValue, drafts),
                FieldOf(record, target, FieldKind.Access, drafts),
                FieldOf(record, target, FieldKind.Description, drafts),
            ]);
    }

    /// <summary>
    /// Whether this template can receive one field of the declaration. The shape rules are the same
    /// ones the details form already respects: an absent property is not created by editing, and a
    /// construct that is preserved read-only stays that way.
    /// </summary>
    private static BulkFieldTarget FieldOf(
        TemplateRecord record, VariableDeclaration declaration, FieldKind field, DraftStore drafts)
    {
        DeclarationField value = declaration.GetField(field);
        string label = TemplateFieldRules.DisplayName(field);

        if (value.Capability == FieldCapability.Missing || !value.Present)
        {
            return new BulkFieldTarget(field, false,
                value.CapabilityReason ?? $"'{declaration.ExactName}' has no {label} property in this template; bulk editing cannot add one.",
                CurrentValue: null, CurrentHasDraft: false);
        }

        if (value.Capability != FieldCapability.Editable)
        {
            return new BulkFieldTarget(field, false,
                value.CapabilityReason ?? $"The {label} of '{declaration.ExactName}' holds a shape that is preserved read-only.",
                value.LogicalValue, CurrentHasDraft: false);
        }

        if (field == FieldKind.Access && !TemplateFieldRules.IsSupportedAccess(value.LogicalValue))
        {
            return new BulkFieldTarget(field, false,
                $"The access of '{declaration.ExactName}' is \"{value.LogicalValue}\", which is outside {TemplateFieldRules.AccessVocabulary}; it stays as it is.",
                value.LogicalValue, CurrentHasDraft: false);
        }

        return new BulkFieldTarget(field, true, Reason: null,
            drafts.EffectiveValue(record.FilePath, declaration, field),
            drafts.HasChange(record.FilePath, declaration.DeclarationId, field));
    }
}
