using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Editing;

/// <summary>
/// The small value vocabulary the editor accepts. Keeping it next to the field model means the
/// parser, the draft layer and the UI agree on what "supported" means.
/// </summary>
public static class TemplateFieldRules
{
    /// <summary>Access values observed in native templates and therefore editable.</summary>
    public static readonly IReadOnlySet<string> SupportedAccessValues =
        new HashSet<string>(StringComparer.Ordinal) { "internal", "external" };

    /// <summary>The same values in a stable order, for a ComboBox that must not reshuffle.</summary>
    public static readonly IReadOnlyList<string> SupportedAccessChoices = ["internal", "external"];

    public static bool IsSupportedAccess(string? value) =>
        value is not null && SupportedAccessValues.Contains(value);

    public static string AccessVocabulary => "internal / external";

    /// <summary>The fields the UI can stage; names are read-only by product decision.</summary>
    public static readonly FieldKind[] StagesFields =
    [
        FieldKind.Access,
        FieldKind.DefaultValue,
        FieldKind.Description,
    ];

    public static string DisplayName(FieldKind field) => field switch
    {
        FieldKind.Name => "name",
        FieldKind.Description => "description",
        FieldKind.DefaultValue => "default value",
        FieldKind.Access => "access",
        _ => field.ToString(),
    };
}
