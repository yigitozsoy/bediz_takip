using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BambuBar.Theme;
using BambuBar.TrayPanel;

namespace BambuBar.AppWindow;

/// <summary>
/// beta's Settings → Appearance: Gamma Player's theme variants (or Windows' light/dark, or Bambu Bar's classic
/// glass) and the accent, as Gamma's preset swatches or a #RRGGBB of the person's own. A change restyles the open
/// windows at once (<see cref="GammaTheme"/> → <see cref="AppTheme.Changed"/>).
/// </summary>
internal static class AppearanceGroup
{
    public static FrameworkElement Build(WindowContext ctx)
    {
        var group = W.Group(Loc.T("Appearance"), "", out var body);

        var theme = new Dropdown(Loc.T("Theme"), ctx.Theme, ThemeEntries) { Text = CurrentThemeName() };
        body.Children.Add(W.Row(Loc.T("Theme"), theme));

        var swatches = new WrapPanel { Orientation = Orientation.Horizontal };
        AutomationProperties.SetName(swatches, Loc.T("Accent color"));
        foreach (var (id, _, _, color) in GammaTheme.Accents)
            swatches.Children.Add(Swatch(color, GammaTheme.AccentLabel(id), GammaTheme.AccentId == id, () => GammaTheme.SetAccent(id)));
        body.Children.Add(W.Row(Loc.T("Accent color"), swatches));

        var custom = new TextField(Loc.T("Custom color (#RRGGBB)"), width: 120)
        {
            Text = GammaTheme.AccentId.StartsWith('#') ? GammaTheme.AccentId : "",
        };
        var apply = W.Button(Loc.T("Apply"), null, small: true);
        apply.Margin = new Thickness(8, 0, 0, 0);
        void Apply()
        {
            var text = custom.Text.Trim();
            if (!text.StartsWith('#')) text = "#" + text;
            if (GammaTheme.ParseAccent(text) is not null) GammaTheme.SetAccent(text);
        }
        apply.Click += (_, _) => Apply();
        custom.OnKeys(Apply);
        body.Children.Add(W.Row(Loc.T("Custom color (#RRGGBB)"),
            new StackPanel { Orientation = Orientation.Horizontal, Children = { custom, apply } }));
        body.Children.Add(W.Note(Loc.T("Themes and accent colors come from Gamma Player. The classic look brings back the original glass and green.")));
        return group;
    }

    private static string CurrentThemeName() => GammaTheme.VariantId switch
    {
        GammaTheme.System => Loc.T("Same as Windows"),
        GammaTheme.Classic => Loc.T("Classic") + " (Bambu Bar)",
        _ => GammaTheme.Fixed is { } v ? GammaTheme.Label(v) : GammaTheme.VariantId,
    };

    private static IReadOnlyList<MenuEntry> ThemeEntries()
    {
        var current = GammaTheme.VariantId;
        var entries = new List<MenuEntry> { new(Loc.T("Same as Windows"), () => GammaTheme.SetVariant(GammaTheme.System), current == GammaTheme.System), MenuEntry.Separator };
        foreach (var dark in new[] { true, false })
        {
            foreach (var v in GammaTheme.Variants.Where(v => v.Dark == dark))
                entries.Add(new MenuEntry(GammaTheme.Label(v), () => GammaTheme.SetVariant(v.Id), current == v.Id, _ => Chip(v)));
            entries.Add(MenuEntry.Separator);
        }
        entries.Add(new MenuEntry(Loc.T("Classic") + " (Bambu Bar)", () => GammaTheme.SetVariant(GammaTheme.Classic), current == GammaTheme.Classic));
        return entries;
    }

    /// <summary>A theme's background with its card on top, for the menu.</summary>
    private static FrameworkElement Chip(GammaTheme.Variant v) => new Border
    {
        Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Palette.Brush(v.Colors.Bg),
        BorderBrush = Palette.Brush(v.Colors.SurfaceHover), BorderThickness = new Thickness(1),
        Child = new Ellipse { Width = 6, Height = 6, Fill = Palette.Brush(GammaTheme.Accent) },
    };

    /// <summary>One accent: a filled circle, ringed when it's the current one.</summary>
    private static ChromeButton Swatch(Color color, string name, bool selected, Action pick)
    {
        var dot = new Ellipse { Width = 22, Height = 22, Fill = Palette.Brush(color) };
        var ring = new Ellipse { Width = 30, Height = 30, StrokeThickness = 2, Visibility = selected ? Visibility.Visible : Visibility.Hidden };
        ring.SetResourceReference(Shape.StrokeProperty, ThemeKeys.TextPrimary);
        var button = new ChromeButton
        {
            Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(17), Margin = new Thickness(0, 0, 4, 0),
            Content = new Grid { Children = { ring, dot } }, Selected = selected, Role = System.Windows.Automation.Peers.AutomationControlType.RadioButton,
        };
        AutomationProperties.SetName(button, name);
        Ui.Tip(button, name);
        button.Click += (_, _) => pick();
        return button;
    }
}
