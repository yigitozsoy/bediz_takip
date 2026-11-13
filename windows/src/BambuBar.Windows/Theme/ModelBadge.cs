using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BambuBar.Core.Imaging;
using BambuBar.Core.Model;

namespace BambuBar.Theme;

/// <summary>
/// beta: under the Gamma theme the printer pictures give way to a badge in the app icon's style: the model's name
/// ("A1", "P2S", "X1C") in Palatino (Georgia's figures sit low, as old-style ones) on a rounded tile, with a smaller line under it where the name has one ("mini",
/// "Pro"). The letters are drawn once per name on the UI thread (<see cref="Prepare"/>) as alpha masks; composing a
/// badge is plain pixel work, so the widgets' writer can do it on its worker.
/// </summary>
public static class ModelBadge
{
    private sealed record Mask(byte[] Alpha, int Width, int Height);

    private static readonly ConcurrentDictionary<string, Mask> Masks = new(StringComparer.Ordinal);
    private const double MaskSize = 256;
    private const string Unknown = "3D";

    /// <summary>The badge's two lines for a model name or code: "X1 Carbon" → ("X1C", null), "A1 mini" → ("A1",
    /// "mini"), "H2D Pro" → ("H2D", "Pro"); an unknown printer → ("3D", null).</summary>
    public static (string Main, string? Sub) Lines(string? model)
    {
        var name = PrinterModel.Named(model)?.Name ?? model?.Replace("Bambu Lab", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 12) return (Unknown, null);
        if (name.Equals("X1 Carbon", StringComparison.OrdinalIgnoreCase)) return ("X1C", null);
        var space = name.IndexOf(' ');
        return space < 0 ? (name, null) : (name[..space], name[(space + 1)..]);
    }

    /// <summary>Draws the letters of every known model (and the fallback). On the UI thread, once.</summary>
    public static void Prepare()
    {
        var lines = PrinterModel.All.Select(m => Lines(m.Name)).Append((Unknown, null));
        foreach (var (main, sub) in lines)
        {
            Ensure(main);
            if (sub is not null) Ensure(sub);
        }
    }

    private static Mask? Ensure(string text)
    {
        if (Masks.TryGetValue(text, out var hit)) return hit;
        // WPF drawing needs an STA (UI) thread; on a worker a name not drawn yet has no badge.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return null;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Palatino Linotype"),
            MaskSize, Brushes.White, 1.0);
        var geometry = formatted.BuildGeometry(new Point(0, 0));
        var bounds = geometry.Bounds;
        if (bounds.IsEmpty) return null;
        var width = (int)Math.Ceiling(bounds.Width) + 4;
        var height = (int)Math.Ceiling(bounds.Height) + 4;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(2 - bounds.X, 2 - bounds.Y));
            dc.DrawGeometry(Brushes.White, null, geometry);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var alpha = new byte[width * height];
        for (var i = 0; i < alpha.Length; i++) alpha[i] = pixels[i * 4 + 3];
        var mask = new Mask(alpha, width, height);
        Masks[text] = mask;
        return mask;
    }

    /// <summary>The badge for <paramref name="scene"/>'s model, <paramref name="width"/> × <paramref name="height"/>
    /// pixels, in the Gamma palette's light or dark variant; null when its letters aren't drawn yet.</summary>
    public static Rgba? Render(PrinterArt.Scene scene, int width, int height, bool dark)
    {
        var (main, sub) = Lines(scene.Model);
        if (Ensure(main) is not { } big) return null;
        var small = sub is null ? null : Ensure(sub);
        var p = GammaTheme.For(dark);
        var tile = SpoolArt.Mix(p.Card, p.Text, dark ? 0.07 : 0.06);
        var ink = p.Text;
        var faint = p.Text2;
        var opacity = scene.Dimmed ? 0.55 : 1.0;

        var image = new Rgba(width, height);
        var side = Math.Min(width, height) * 0.86;
        double left = (width - side) / 2, top = (height - side) / 2, radius = side * 0.21;
        // The tile, its corners smoothed over a pixel.
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var coverage = RoundedCoverage(x + 0.5, y + 0.5, left, top, side, radius);
                if (coverage > 0) Blend(image, x, y, tile, coverage * opacity);
            }
        }
        // The name: its capitals 30 % of the tile (24 % with a second line), never wider than 72 %; the second line
        // at 42 % of that size, under it.
        var scale = side * (small is null ? 0.30 : 0.24) / (big.Height - 4);
        scale = Math.Min(scale, side * 0.72 / (big.Width - 4));
        var subScale = scale * 0.42;
        var mainHeight = big.Height * scale;
        var subHeight = small is null ? 0 : small.Height * subScale;
        var gap = small is null ? 0 : side * 0.05;
        var blockTop = top + (side - mainHeight - gap - subHeight) / 2;
        Stamp(image, big, scale, left + (side - big.Width * scale) / 2, blockTop, ink, opacity);
        if (small is not null)
            Stamp(image, small, subScale, left + (side - small.Width * subScale) / 2, blockTop + mainHeight + gap, faint, opacity);
        return image;
    }

    /// <summary>The badge as a WPF image (the window and the panel), for the current light or dark variant.</summary>
    public static BitmapSource? Bitmap(PrinterArt.Scene scene, int width, int height, double dpi) =>
        Render(scene, width, height, AppTheme.Current != ThemeVariant.Light) is { } rgba ? ArtImage.ToBitmap(rgba, dpi) : null;

    private static double RoundedCoverage(double x, double y, double left, double top, double side, double radius)
    {
        // Signed distance to the rounded square; one pixel of soft edge.
        double cx = left + side / 2, cy = top + side / 2, half = side / 2 - radius;
        double dx = Math.Max(Math.Abs(x - cx) - half, 0), dy = Math.Max(Math.Abs(y - cy) - half, 0);
        var distance = Math.Sqrt(dx * dx + dy * dy) - radius;
        return Math.Clamp(0.5 - distance, 0, 1);
    }

    /// <summary>The mask scaled by <paramref name="scale"/> with its top-left at (<paramref name="x0"/>,
    /// <paramref name="y0"/>), sampled bilinearly, in <paramref name="color"/>.</summary>
    private static void Stamp(Rgba image, Mask mask, double scale, double x0, double y0, Color color, double opacity)
    {
        int fromX = Math.Max(0, (int)x0), toX = Math.Min(image.Width, (int)Math.Ceiling(x0 + mask.Width * scale));
        int fromY = Math.Max(0, (int)y0), toY = Math.Min(image.Height, (int)Math.Ceiling(y0 + mask.Height * scale));
        for (var y = fromY; y < toY; y++)
        {
            for (var x = fromX; x < toX; x++)
            {
                var a = Sample(mask, (x + 0.5 - x0) / scale - 0.5, (y + 0.5 - y0) / scale - 0.5);
                if (a > 0) Blend(image, x, y, color, a / 255.0 * opacity);
            }
        }
    }

    private static double Sample(Mask mask, double x, double y)
    {
        int x1 = (int)Math.Floor(x), y1 = (int)Math.Floor(y);
        double fx = x - x1, fy = y - y1;
        double At(int px, int py) => px < 0 || py < 0 || px >= mask.Width || py >= mask.Height ? 0 : mask.Alpha[py * mask.Width + px];
        return (At(x1, y1) * (1 - fx) + At(x1 + 1, y1) * fx) * (1 - fy) + (At(x1, y1 + 1) * (1 - fx) + At(x1 + 1, y1 + 1) * fx) * fy;
    }

    /// <summary>Straight-alpha "over".</summary>
    private static void Blend(Rgba image, int x, int y, Color color, double alpha)
    {
        if (alpha <= 0) return;
        var i = (y * image.Width + x) * 4;
        var px = image.Pixels;
        double da = px[i + 3] / 255.0, sa = Math.Min(1, alpha);
        var outA = sa + da * (1 - sa);
        if (outA <= 0) return;
        px[i] = (byte)Math.Round((color.R * sa + px[i] * da * (1 - sa)) / outA);
        px[i + 1] = (byte)Math.Round((color.G * sa + px[i + 1] * da * (1 - sa)) / outA);
        px[i + 2] = (byte)Math.Round((color.B * sa + px[i + 2] * da * (1 - sa)) / outA);
        px[i + 3] = (byte)Math.Round(outA * 255);
    }
}
