using System.Globalization;
using System.Windows.Media;
using BambuBar.Core.App;

namespace BambuBar.Theme;

/// <summary>
/// beta's look, taken from Gamma Player (gammaplayer/theme.py): AMOLED black and nine other variants, flat
/// surfaces instead of glass, and an accent the person picks (a named one or their own #RRGGBB). "classic" keeps Bambu
/// Bar's Fluent glass and Mica; "system" follows Windows' light/dark with the AMOLED and White variants.
/// <para>Read once at startup (<see cref="Load"/>); <see cref="SetVariant"/> and <see cref="SetAccent"/> save and
/// restyle the open panel and window through <see cref="AppTheme.Changed"/>.</para>
/// </summary>
public static class GammaTheme
{
    public const string ThemeKey = "betaMonitorTheme";
    public const string AccentKey = "betaMonitorAccent";
    public const string DefaultVariant = "bambu";
    public const string DefaultAccent = "bambu";
    public const string System = "system";
    public const string Classic = "classic";

    /// <summary>Gamma's roles. Text, surfaces and overlays; the accent is separate.</summary>
    public sealed record Palette(
        Color Bg, Color Bg2, Color Surface, Color SurfaceHover, Color Card, Color Text, Color Text2, Color Text3,
        Color Divider, Color Hover, Color Pressed, Color Selected, Color InputBg, Color InputBorder, Color ButtonBg,
        Color ButtonFg, Color Scroll);

    public sealed record Variant(string Id, string Turkish, string English, bool Dark, Palette Colors);

    private static Color Hex(string hex)
    {
        var v = uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return hex.Length == 9
            ? Color.FromArgb((byte)v, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8))
            : Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static Color White(double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), 255, 255, 255);
    private static Color Black(double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0);

    private static readonly Palette DarkBase = new(
        Bg: Hex("#000000"), Bg2: Hex("#000000"), Surface: Hex("#272727"), SurfaceHover: Hex("#3f3f3f"), Card: Hex("#212121"),
        Text: Hex("#f1f1f1"), Text2: Hex("#aaaaaa"), Text3: Hex("#717171"), Divider: White(0.10), Hover: White(0.10),
        Pressed: White(0.18), Selected: White(0.14), InputBg: Hex("#121212"), InputBorder: Hex("#303030"),
        ButtonBg: Hex("#f1f1f1"), ButtonFg: Hex("#0f0f0f"), Scroll: White(0.22));

    private static readonly Palette LightBase = new(
        Bg: Hex("#ffffff"), Bg2: Hex("#f9f9f9"), Surface: Hex("#f2f2f2"), SurfaceHover: Hex("#e5e5e5"), Card: Hex("#ffffff"),
        Text: Hex("#0f0f0f"), Text2: Hex("#606060"), Text3: Hex("#909090"), Divider: Black(0.10), Hover: Black(0.05),
        Pressed: Black(0.10), Selected: Black(0.07), InputBg: Hex("#ffffff"), InputBorder: Hex("#d3d3d3"),
        ButtonBg: Hex("#0f0f0f"), ButtonFg: Hex("#ffffff"), Scroll: Black(0.25));

    /// <summary>Gamma's VARIANTS, in its order.</summary>
    public static IReadOnlyList<Variant> Variants { get; } =
    [
        // beta's default: the silver of Bambu Lab's printers (the H2D's shell, #9e9e9e, and its lit edge, #d2d2ce),
        // in Gamma's shapes.
        new("bambu", "Bambu grisi", "Bambu gray", false, LightBase with
        {
            Bg = Hex("#9e9e9e"), Bg2 = Hex("#a6a6a6"), Surface = Hex("#c4c4c0"), SurfaceHover = Hex("#b8b8b4"), Card = Hex("#d2d2ce"),
            Text = Hex("#141414"), Text2 = Hex("#3a3a3a"), Text3 = Hex("#5a5a5a"), Divider = Black(0.12), Hover = Black(0.07),
            Pressed = Black(0.13), Selected = Black(0.10), InputBg = Hex("#e2e2df"), InputBorder = Hex("#8a8a88"),
        }),
        new("amoled", "AMOLED siyah", "AMOLED black", true, DarkBase),
        new("dark_gray", "Koyu gri", "Dark gray", true, DarkBase with { Bg = Hex("#0f0f0f"), Bg2 = Hex("#181818") }),
        new("graphite", "Grafit", "Graphite", true, DarkBase with
        {
            Bg = Hex("#1c1c1e"), Bg2 = Hex("#232326"), Surface = Hex("#2f2f33"), SurfaceHover = Hex("#45454a"), Card = Hex("#2a2a2e"),
            InputBg = Hex("#18181a"), InputBorder = Hex("#3a3a3e"),
        }),
        new("midnight", "Gece mavisi", "Midnight blue", true, DarkBase with
        {
            Bg = Hex("#0d1117"), Bg2 = Hex("#111723"), Surface = Hex("#1f2733"), SurfaceHover = Hex("#2d3846"), Card = Hex("#161d29"),
            InputBg = Hex("#0b0f16"), InputBorder = Hex("#2a3442"), Text2 = Hex("#9aa6b4"),
        }),
        new("mocha", "Sıcak koyu", "Mocha", true, DarkBase with
        {
            Bg = Hex("#17130f"), Bg2 = Hex("#1e1914"), Surface = Hex("#2c251e"), SurfaceHover = Hex("#3d342a"), Card = Hex("#241e18"),
            InputBg = Hex("#120f0c"), InputBorder = Hex("#3a3128"), Text2 = Hex("#b0a594"),
        }),
        new("white", "Beyaz", "White", false, LightBase),
        new("light_gray", "Açık gri", "Light gray", false, LightBase with
        {
            Bg = Hex("#f1f1f3"), Bg2 = Hex("#e9e9ec"), Surface = Hex("#e2e2e6"), SurfaceHover = Hex("#d5d5da"), Card = Hex("#fafafa"),
        }),
        new("cream", "Krem", "Cream", false, LightBase with
        {
            Bg = Hex("#faf6ef"), Bg2 = Hex("#f3eee4"), Surface = Hex("#ece5d8"), SurfaceHover = Hex("#e0d7c6"), Card = Hex("#fffdf8"),
            InputBg = Hex("#fffdf8"), Text2 = Hex("#6b6258"),
        }),
        new("ice", "Buz mavisi", "Ice blue", false, LightBase with
        {
            Bg = Hex("#f3f7fb"), Bg2 = Hex("#e9f0f7"), Surface = Hex("#e1e9f2"), SurfaceHover = Hex("#d3deea"), Text2 = Hex("#56657a"),
        }),
        new("mint", "Nane", "Mint", false, LightBase with
        {
            Bg = Hex("#f2f8f4"), Bg2 = Hex("#e7f2eb"), Surface = Hex("#dcebe2"), SurfaceHover = Hex("#cde0d4"), Text2 = Hex("#56695e"),
        }),
    ];

    /// <summary>Gamma's ACCENTS, after Bambu Lab's green (beta's default: Gamma's "gray" reads as idle here).</summary>
    public static IReadOnlyList<(string Id, string Turkish, string English, Color Color)> Accents { get; } =
    [
        ("bambu", "Bambu yeşili", "Bambu green", Hex("#00ae42")),
        ("blue", "Mavi", "Blue", Hex("#3ea6ff")),
        ("red", "Kırmızı", "Red", Hex("#ff0033")),
        ("green", "Yeşil", "Green", Hex("#2ecc71")),
        ("purple", "Mor", "Purple", Hex("#a970ff")),
        ("orange", "Turuncu", "Orange", Hex("#ff8a1f")),
        ("pink", "Pembe", "Pink", Hex("#ff4fa3")),
        ("teal", "Turkuaz", "Teal", Hex("#1fc7b6")),
        ("gray", "Gri", "Gray", Hex("#c4c4c4")),
    ];

    /// <summary>Bambu Bar's own green, for <see cref="Classic"/>.</summary>
    public static readonly Color ClassicAccent = Color.FromRgb(0x21, 0xCC, 0x6B);

    private static AppSettings? _settings;

    /// <summary>The chosen variant's id, <see cref="System"/> or <see cref="Classic"/>.</summary>
    public static string VariantId { get; private set; } = DefaultVariant;

    /// <summary>A named accent or "#rrggbb".</summary>
    public static string AccentId { get; private set; } = DefaultAccent;

    /// <summary>Bumped on every change, so views rebuild even when light/dark stays the same (AMOLED → Midnight).</summary>
    public static int Stamp { get; private set; }

    /// <summary>Gamma's look is on (anything but <see cref="Classic"/>).</summary>
    public static bool Active => VariantId != Classic;

    /// <summary>The variant Windows' light/dark doesn't decide: null for <see cref="System"/> and <see cref="Classic"/>.</summary>
    public static Variant? Fixed => Variants.FirstOrDefault(v => v.Id == VariantId);

    public static Color Accent => !Active ? ClassicAccent : ParseAccent(AccentId) ?? Accents[0].Color;

    /// <summary>The accent's prominent-button shade (Bambu Bar's AccentDeep).</summary>
    public static Color AccentDeep => SpoolArt.Mix(Accent, Colors.Black, 0.25);

    /// <summary>Text on an accent fill: Gamma's near-black on bright accents, white on dark ones.</summary>
    public static Color OnAccent => Luminance(Accent) > 0.55 ? Color.FromRgb(0x0f, 0x0f, 0x0f) : Colors.White;

    public static Color? ParseAccent(string? id)
    {
        if (id is null) return null;
        foreach (var a in Accents) if (a.Id == id) return a.Color;
        if (id.Length == 7 && id[0] == '#' && uint.TryParse(id[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return Hex(id);
        return null;
    }

    /// <summary>Reads the saved choice (the app calls it before building any window).</summary>
    public static void Load(AppSettings settings)
    {
        _settings = settings;
        var variant = settings.Get<string>(ThemeKey) ?? DefaultVariant;
        VariantId = variant is System or Classic || Variants.Any(v => v.Id == variant) ? variant : DefaultVariant;
        var accent = settings.Get<string>(AccentKey) ?? DefaultAccent;
        AccentId = ParseAccent(accent) is null ? DefaultAccent : accent.ToLowerInvariant();
        // The model badges' letters, drawn here on the UI thread for the widgets' writer to use on its own.
        ModelBadge.Prepare();
        PublishWidgetStyle();
    }

    /// <summary>The desktop widgets follow the theme: always a dark card (the skins draw light text), from the picked
    /// dark variant or AMOLED, with the accent. The classic look leaves the skins' own glass.</summary>
    private static void PublishWidgetStyle()
    {
        static string Rgb(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";
        static string Rgba(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
        if (!Active)
        {
            Core.Widgets.WidgetStyle.Current = null;
            Core.Imaging.PrinterArt.Badge = null;
            return;
        }
        // The widgets show the model's badge on their dark card.
        Core.Imaging.PrinterArt.Badge = (scene, width, height) => ModelBadge.Render(scene, width, height, dark: true);
        Core.Imaging.PrinterArt.BadgeKey = $"{VariantId}|{AccentId}";
        var p = For(dark: true);
        Core.Widgets.WidgetStyle.Current = new Core.Widgets.WidgetState.StyleView(
            Accent: Rgb(Accent),
            CardTop: Rgb(SpoolArt.Mix(p.Bg, p.Card, 0.7)), CardBottom: Rgb(p.Bg),
            CardHoverTop: Rgb(p.Card), CardHoverBottom: Rgb(SpoolArt.Mix(p.Bg, p.Card, 0.35)),
            Border: 0.07, Text: Rgba(p.Text), Sec: Rgba(p.Text2), Tile: Rgba(p.Surface), Track: Rgba(p.SurfaceHover))
        {
            Preparing = Rgb(BambuBar.Theme.Palette.Preparing), Paused = Rgb(BambuBar.Theme.Palette.Paused), Failed = Rgb(BambuBar.Theme.Palette.Failed),
            Finished = Rgb(BambuBar.Theme.Palette.Finished), Idle = Rgb(BambuBar.Theme.Palette.Idle), Nozzle = Rgb(BambuBar.Theme.Palette.Nozzle), Bed = Rgb(BambuBar.Theme.Palette.Bed),
            Chamber = Rgb(BambuBar.Theme.Palette.Chamber),
        };
    }

    public static void SetVariant(string id)
    {
        if (id == VariantId || !(id is System or Classic || Variants.Any(v => v.Id == id))) return;
        VariantId = id;
        _settings?.Set(ThemeKey, id);
        PublishWidgetStyle();
        Stamp++;
        AppTheme.NotifyChanged();
    }

    public static void SetAccent(string id)
    {
        if (ParseAccent(id) is null || id.Equals(AccentId, StringComparison.OrdinalIgnoreCase)) return;
        AccentId = id.ToLowerInvariant();
        _settings?.Set(AccentKey, AccentId);
        PublishWidgetStyle();
        Stamp++;
        AppTheme.NotifyChanged();
    }

    /// <summary>The palette for a light or dark build: the fixed variant when it is of that kind, else AMOLED or White.</summary>
    public static Palette For(bool dark) =>
        Fixed is { } v && v.Dark == dark ? v.Colors : dark ? DarkBase : LightBase;

    public static string Label(Variant v) => Core.Loc.Language == "tr" ? v.Turkish : v.English;

    public static string AccentLabel(string id) =>
        Accents.FirstOrDefault(a => a.Id == id) is { Id: not null } a ? (Core.Loc.Language == "tr" ? a.Turkish : a.English) : id;

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
}
