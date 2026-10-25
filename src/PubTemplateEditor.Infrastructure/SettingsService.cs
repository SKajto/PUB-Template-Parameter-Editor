using System.Text.Json;

namespace PubTemplateEditor.Infrastructure;

public enum ThemeChoice
{
    Light,
    Dark,
}

/// <summary>Persisted user preferences. Saved immediately when changed; drafts are never persisted automatically.</summary>
public sealed class EditorSettings
{
    public string? Theme { get; set; }
    public string? LastFolder { get; set; }
}

/// <summary>
/// User-scoped settings under LocalApplicationData. In an MSIX package this path is
// identity-scoped by Windows; nothing is written beside the executable.
/// </summary>
public sealed class SettingsService
{
    private readonly string _directory;

    /// <summary>Defaults to the user-scoped directory; tests inject a temporary one.</summary>
    public SettingsService(string? baseDirectory = null)
    {
        _directory = string.IsNullOrWhiteSpace(baseDirectory) ? AppDataDirectory : baseDirectory!;
    }

    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PubTemplateEditor");

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public EditorSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<EditorSettings>(json) ?? new EditorSettings();
            }
        }
        catch (Exception)
        {
            // A corrupt settings file must not prevent startup; fall back to defaults.
        }

        return new EditorSettings();
    }

    public void Save(EditorSettings settings)
    {
        Directory.CreateDirectory(_directory);
        string tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, SettingsPath, overwrite: true);
    }

    public static ThemeChoice? ParseTheme(string? name) =>
        Enum.TryParse<ThemeChoice>(name, ignoreCase: true, out var t) ? t : null;
}
