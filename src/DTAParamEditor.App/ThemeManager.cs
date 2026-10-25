using System.Windows;
using DTAParamEditor.Infrastructure;

namespace DTAParamEditor.App;

/// <summary>Swaps the merged theme dictionary. All views bind through DynamicResource.</summary>
public static class ThemeManager
{
    public static void Apply(Application app, ThemeChoice theme)
    {
        var dicts = ((ResourceDictionary)app.Resources).MergedDictionaries;
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml", UriKind.Absolute),
        };

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
