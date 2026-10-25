using System.Windows;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App;

/// <summary>Swaps the merged theme dictionary. All views bind through DynamicResource.</summary>
public static class ThemeManager
{
    public static void Apply(Application app, ThemeChoice theme)
    {
        ApplyResolved(app, Resolve(app, theme));
    }

    /// <summary>System means "whatever Windows currently prefers"; resolved per call.</summary>
    public static ThemeChoice Resolve(Application app, ThemeChoice theme) =>
        theme == ThemeChoice.System ? SystemThemePreference.Current() : theme;

    private static void ApplyResolved(Application app, ThemeChoice theme)
    {
        // Assembly-qualified component URI: resolves against this assembly's compiled
        // resources regardless of which executable hosts the assembly.
        var replacement = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/{typeof(ThemeManager).Assembly.GetName().Name};component/Themes/{theme}.xaml",
                UriKind.Absolute),
        };
        var dicts = ((ResourceDictionary)app.Resources).MergedDictionaries;

        if (dicts.Count > 0)
        {
            dicts[0] = replacement;
        }
        else
        {
            dicts.Add(replacement);
        }
    }
}
