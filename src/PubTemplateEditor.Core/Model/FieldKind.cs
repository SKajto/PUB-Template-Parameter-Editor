namespace PubTemplateEditor.Core.Model;

/// <summary>The four UI fields of a declared variable. UI Internal/External maps to the access property, not the XML type property.</summary>
public enum FieldKind
{
    Name,
    Description,
    DefaultValue,
    Access,
}
