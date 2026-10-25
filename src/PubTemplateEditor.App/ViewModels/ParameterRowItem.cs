using System.Collections.ObjectModel;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Model;
using PubTemplateEditor.Core.Search;

namespace PubTemplateEditor.App.ViewModels;

/// <summary>
/// One declaration row of the active template's parameter table. Values are shown as
/// text previews; the full value is carried separately for tooltips and editing.
/// </summary>
public sealed class ParameterRowItem : ViewModelBase
{
    public ParameterRowItem(VariableDeclaration declaration)
    {
        Declaration = declaration;
    }

    public VariableDeclaration Declaration { get; }

    public int DeclarationId => Declaration.DeclarationId;

    public string Name => Declaration.DisplayName;

    public string AccessLabel => Declaration.Access.LogicalValue ?? "Unknown";

    public string XmlTypeLabel => Declaration.XmlTypeProperty ?? "(missing)";

    /// <summary>True when any field exists but cannot be edited losslessly (read-only shape).</summary>
    public bool HasReadOnlyShape =>
        Declaration.Name.Capability == FieldCapability.ReadOnlyUnsupported
        || Declaration.Description.Capability == FieldCapability.ReadOnlyUnsupported
        || Declaration.DefaultValue.Capability == FieldCapability.ReadOnlyUnsupported
        || Declaration.Access.Capability == FieldCapability.ReadOnlyUnsupported;

    public string ReadOnlyShapeReasons => string.Join(
        "; ",
        new[] { Declaration.Name, Declaration.Description, Declaration.DefaultValue, Declaration.Access }
            .Where(f => f.Capability == FieldCapability.ReadOnlyUnsupported)
            .Select(f => $"{f.Kind}: {f.CapabilityReason}"));

    public string DefaultPreview => Preview(DeclarationFieldText(Declaration.DefaultValue));

    public string DescriptionPreview => Preview(DeclarationFieldText(Declaration.Description));

    public string DefaultFullValue => DeclarationFieldText(Declaration.DefaultValue);

    public string DescriptionFullValue => DeclarationFieldText(Declaration.Description);

    /// <summary>Empty values stay visibly distinct from missing ones (REQ: no coercion).</summary>
    private static string DeclarationFieldText(DeclarationField field) =>
        !field.Present ? "(missing)" : field.LogicalValue ?? string.Empty;

    private static string Preview(string value)
    {
        if (value.Length == 0)
        {
            return "(empty)";
        }

        string single = value.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= 160 ? single : single[..160] + "…";
    }
}
