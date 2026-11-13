using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace BambuBar.Theme;

/// <summary>
/// beta: Wi-Fi strength as Windows draws it: a dot with three arcs fanning up from it. <see cref="Level"/> 1–4 fills
/// that many parts (the dot first); the rest stay faint. Decorative: the text beside it names the strength for
/// screen readers.
/// </summary>
public sealed class WifiBars : FrameworkElement
{
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(nameof(Ink), typeof(Brush), typeof(WifiBars),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private int _level;

    public WifiBars()
    {
        Width = 14;
        Height = 11;
        SetResourceReference(InkProperty, ThemeKeys.TextSecondary);
    }

    public Brush? Ink { get => (Brush?)GetValue(InkProperty); set => SetValue(InkProperty, value); }

    /// <summary>Parts lit, 0–4.</summary>
    public int Level
    {
        get => _level;
        set { value = Math.Clamp(value, 0, 4); if (_level != value) { _level = value; InvalidateVisual(); } }
    }

    /// <summary>Lit parts for a signal in dBm, on the same steps as the panel's words (excellent 4 … poor 1).</summary>
    public static int For(int dbm) => dbm switch
    {
        >= -55 and <= 0 => 4,
        >= -67 => 3,
        >= -75 => 2,
        _ => 1,
    };

    protected override AutomationPeer OnCreateAutomationPeer() => null!;

    protected override void OnRender(DrawingContext dc)
    {
        if (Ink is not { } ink) return;
        var faint = ink.Clone();
        faint.Opacity = 0.28;
        faint.Freeze();
        var c = new Point(Width / 2, Height - 1.2);
        dc.DrawEllipse(_level >= 1 ? ink : faint, null, c, 1.3, 1.3);
        double[] radii = [4.2, 7.0, 9.8];
        for (var i = 0; i < radii.Length; i++)
        {
            var pen = new Pen(_level >= i + 2 ? ink : faint, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            var r = radii[i];
            var a = Math.PI / 4;
            var from = new Point(c.X - r * Math.Sin(a), c.Y - r * Math.Cos(a));
            var to = new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(from, false, false);
                ctx.ArcTo(to, new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }
    }
}
