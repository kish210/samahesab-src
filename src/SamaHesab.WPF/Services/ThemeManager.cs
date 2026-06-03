using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SamaHesab.WPF.Services;

/// <summary>
/// Runtime-switchable Telerik theme system. The Telerik NoXaml theme dictionaries
/// are merged here (not in App.xaml) so they can be swapped at runtime. They are
/// always inserted BEFORE the app's own Sama dictionaries (Base/Styles/…) so the
/// custom styling keeps priority for standard controls.
/// </summary>
public static class ThemeManager
{
    public static readonly string[] Available = { "Office2019", "Fluent", "Crystal", "Windows11" };

    public static string Current { get; private set; } = "Office2019";

    private static readonly string[] Files =
    {
        "Telerik.Windows.Controls",
        "Telerik.Windows.Controls.Input",
        "Telerik.Windows.Controls.Navigation",
        "Telerik.Windows.Controls.RibbonView",
        "Telerik.Windows.Controls.Docking",
        "Telerik.Windows.Controls.GridView",
        "Telerik.Windows.Controls.Data",
    };

    private static readonly List<ResourceDictionary> _added = new();

    public static void Apply(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme) || !Available.Contains(theme))
            theme = "Office2019";

        var app = System.Windows.Application.Current;
        if (app == null) return;
        var dicts = app.Resources.MergedDictionaries;

        // remove previously-added Telerik theme dictionaries
        foreach (var d in _added) dicts.Remove(d);
        _added.Clear();

        // insert before the first Sama custom dictionary (Base.xaml)
        int idx = 0;
        for (int i = 0; i < dicts.Count; i++)
        {
            var src = dicts[i].Source?.OriginalString ?? "";
            if (src.Contains("Assets/Themes/Base.xaml")) { idx = i; break; }
            idx = dicts.Count;
        }

        foreach (var f in Files)
        {
            var d = new ResourceDictionary
            {
                Source = new System.Uri(
                    $"pack://application:,,,/Telerik.Windows.Themes.{theme};component/Themes/{f}.xaml")
            };
            dicts.Insert(idx++, d);
            _added.Add(d);
        }

        Current = theme;
    }
}
