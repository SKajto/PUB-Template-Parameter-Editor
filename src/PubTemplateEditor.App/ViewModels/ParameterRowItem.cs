using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// One row of the parameter table. The row belongs to the active template, so it shows the
/// session state: staged values are displayed instead of stored values, and a dirty marker says
/// which fields carry an unsaved change. Nothing here writes; the values live only in memory.
/// </summary>
public sealed class ParameterRowItem : ViewModelBase
{
    private readonly DraftStore _drafts;

    public ParameterRowItem(VariableDeclaration declaration, TemplateItem file, DraftStore drafts)
    {
        Declaration = declaration;
        File = file;
        _drafts = drafts;
    }

    public VariableDeclaration Declaration { get; }

    public TemplateItem File { get; }

    public int DeclarationId => Declaration.DeclarationId;

    /// <summary>Parameter names are read-only and never used as a bulk key from this row.</summary>
    public string Name => Declaration.ExactName ?? string.Empty;

    public string XmlTypeLabel => Declaration.XmlTypeProperty ?? "(no type property)";

    public string? RawAccess => Declaration.Access.LogicalValue;

    // ------------------------------------------------------- effective (draft-aware) view

    public string? EffectiveDefaultValue =>
        _drafts.EffectiveValue(File.FilePath, Declaration, FieldKind.DefaultValue);

    public string? EffectiveAccess =>
        _drafts.EffectiveValue(File.FilePath, Declaration, FieldKind.Access);

    public string? EffectiveDescription =>
        _drafts.EffectiveValue(File.FilePath, Declaration, FieldKind.Description);

    public string AccessLabel => EffectiveAccess ?? "(missing)";

    public string DefaultPreview => Preview(EffectiveDefaultValue);

    public string DescriptionPreview => Preview(EffectiveDescription);

    public string DefaultFullValue => EffectiveDefaultValue ?? "(no default property)";

    public string DescriptionFullValue => EffectiveDescription ?? "(no description property)";

    /// <summary>Fields with a staged, not-yet-saved change; drives the row's dirty badge.</summary>
    public IReadOnlyList<FieldKind> DraftedFields
    {
        get
        {
            List<FieldKind> fields = [];
            foreach (FieldKind field in TemplateFieldRules.StagesFields)
            {
                if (_drafts.HasChange(File.FilePath, DeclarationId, field))
                {
                    fields.Add(field);
                }
            }

            return fields;
        }
    }

    public bool HasDrafts => DraftedFields.Count > 0;

    public int StagedEditCount => DraftedFields.Count;

    /// <summary>"default, access" style summary shown next to the dirty badge.</summary>
    public string DraftedFieldsSummary =>
        string.Join(", ", DraftedFields.Select(TemplateFieldRules.DisplayName));

    public bool HasReadOnlyShape =>
        Declaration.Name.Capability != FieldCapability.Editable ||
        Declaration.Description.Capability != FieldCapability.Editable ||
        Declaration.DefaultValue.Capability != FieldCapability.Editable ||
        Declaration.Access.Capability != FieldCapability.Editable;

    public string ReadOnlyShapeReasons => string.Join(" ",
        new[]
        {
            Declaration.Name.CapabilityReason,
            Declaration.Description.CapabilityReason,
            Declaration.DefaultValue.CapabilityReason,
            Declaration.Access.CapabilityReason,
        }.Where(notEmpty => !string.IsNullOrWhiteSpace(notEmpty)));

    /// <summary>Re-raises the draft-derived bindings after staging, discarding or re-reading.</summary>
    public void RefreshDraftState()
    {
        foreach (string property in new[]
                 {
                     nameof(EffectiveDefaultValue),
                     nameof(EffectiveAccess),
                     nameof(EffectiveDescription),
                     nameof(AccessLabel),
                     nameof(DefaultPreview),
                     nameof(DescriptionPreview),
                     nameof(DefaultFullValue),
                     nameof(DescriptionFullValue),
                     nameof(HasDrafts),
                     nameof(StagedEditCount),
                     nameof(DraftedFieldsSummary),
                     nameof(RowTooltip),
                 })
        {
            Raise(property);
        }
    }

    /// <summary>
    /// What the parameter really holds, shown while the pointer rests on a row. The table cells are
    /// truncated to fit, so a reviewer needs the name, the description and the value without widening
    /// every column, and needs to see which of them are staged rather than stored.
    /// </summary>
    public string RowTooltip
    {
        get
        {
            var lines = new List<string>
            {
                Name.Length == 0 ? "(parameter without a name)" : Name,
                $"Current value: {DefaultFullValue}",
                $"Description: {DescriptionFullValue}",
                $"Access: {AccessLabel}",
            };

            if (StagedEditCount > 0)
            {
                lines.Add($"Staged in this session, not saved: {DraftedFieldsSummary}");
            }

            if (HasReadOnlyShape)
            {
                lines.Add($"Kept read-only: {ReadOnlyShapeReasons}");
            }

            lines.Add($"{File.FileName} · declaration #{DeclarationId}");
            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string Preview(string? value)
    {
        if (value is null)
        {
            return "(missing)";
        }

        if (value.Length <= 48)
        {
            return value;
        }

        return value[..45] + "…";
    }
}
