using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using BambuBar.Core;
using BambuBar.Core.App;
using BambuBar.Core.Model;
using BambuBar.Theme;

namespace BambuBar.AppWindow;

/// <summary>
/// beta's History page: totals (prints, success rate, time spent printing, the last week), how they ended and
/// which materials, then every print the app saw end, newest first. The store saves up to
/// <see cref="HistoryStore.BetaMonitorLimit"/> per printer.
/// </summary>
internal sealed class HistoryPage(WindowContext ctx) : Page(ctx)
{
    /// <summary>Rows in the list; older prints still count in the totals.</summary>
    private const int Shown = 100;

    private readonly Region _content = new();

    public override FrameworkElement Build()
    {
        Refresh();
        return new Stack
        {
            Spacing = 22,
            Children =
            {
                W.PageHeader(Loc.T("Print History"), Loc.T("Every print beta saw end, and how it went.")),
                _content.Host,
            },
        };
    }

    public override void Refresh()
    {
        var history = Store.History;
        var key = $"{history.Count}|{history.FirstOrDefault()?.Id}|{history.FirstOrDefault()?.Outcome}|{Store.Sessions.Count}";
        _content.Show(key, () => Content(history));
    }

    private UIElement Content(IReadOnlyList<JobRecord> history)
    {
        var ended = history.Where(j => j.Outcome != JobOutcome.Running).ToList();
        if (ended.Count == 0)
            return W.Empty(AppPage.History.Glyph(), Loc.T("No prints yet"), Loc.T("Prints show up here once beta sees one end."));

        var stats = HistoryStats.Of(ended, DateTimeOffset.Now);
        var tiles = new UniformGrid { Columns = 4 };
        tiles.Children.Add(Tile(Loc.T("Prints"), stats.Total.ToString(CultureInfo.CurrentCulture)));
        tiles.Children.Add(Tile(Loc.T("Success rate"), stats.SuccessRate is { } rate ? rate.ToString("P0", CultureInfo.CurrentCulture) : "—"));
        tiles.Children.Add(Tile(Loc.T("Print time"), Format.Duration(stats.PrintTime)));
        tiles.Children.Add(Tile(Loc.T("Last 7 days"), stats.LastWeek.ToString(CultureInfo.CurrentCulture)));

        var summary = W.Group(Loc.T("Summary"), "", out var body);
        body.Children.Add(W.DetailRow(JobOutcome.Finished.Title(), stats.Finished.ToString(CultureInfo.CurrentCulture), Dot(Palette.Finished)));
        body.Children.Add(W.DetailRow(JobOutcome.Failed.Title(), stats.Failed.ToString(CultureInfo.CurrentCulture), Dot(Palette.Failed)));
        body.Children.Add(W.DetailRow(JobOutcome.Canceled.Title(), stats.Canceled.ToString(CultureInfo.CurrentCulture), Dot(Palette.Idle)));
        if (stats.Unknown > 0)
            body.Children.Add(W.DetailRow(Loc.T("Ended while beta was closed"), stats.Unknown.ToString(CultureInfo.CurrentCulture), Dot(Palette.Idle)));
        if (stats.Longest is { Duration: { } longest } job)
            body.Children.Add(W.DetailRow(Loc.T("Longest print"), $"{job.Name} · {Format.Duration(longest)}"));
        if (stats.Materials.Count > 0)
            body.Children.Add(W.DetailRow(Loc.T("Materials"), string.Join(" · ", stats.Materials.Select(m => $"{m.Material} {m.Count}"))));

        var list = W.Group(Loc.T("Recent prints"), AppPage.History.Glyph(), out var rows);
        var names = Store.Sessions.ToDictionary(s => s.Id, s => s.Config.Name, StringComparer.Ordinal);
        var multiple = names.Count > 1;
        foreach (var record in ended.OrderByDescending(j => j.EndedAt ?? j.StartedAt).Take(Shown))
            rows.Children.Add(Row(record, multiple && record.PrinterSerial is { } serial ? names.GetValueOrDefault(serial) : null));
        if (ended.Count > Shown)
            rows.Children.Add(W.Note(Loc.F("And %@ older prints, counted above.", (ended.Count - Shown).ToString(CultureInfo.CurrentCulture))));

        return new Stack { Spacing = 22, Children = { tiles, summary, list } };
    }

    /// <summary>A number with what it counts under it.</summary>
    private static FrameworkElement Tile(string label, string value)
    {
        var number = Ui.Tabular(Ui.Text(value, 24, FontWeights.SemiBold, display: true));
        var caption = Ui.Text(label, WRamp.Note, null, ThemeKeys.TextSecondary);
        var card = W.Card(new Stack { Spacing = 2, Children = { number, caption } });
        card.Margin = new Thickness(0, 0, 10, 0);
        return Ui.ReadAsOne(card, $"{label}: {value}");
    }

    /// <summary>One print: its filament's colour, name, printer and when; how long and how it ended on the right.</summary>
    private FrameworkElement Row(JobRecord job, string? printer)
    {
        var grid = Ui.Columns("Auto", "*", "Auto");
        var swatch = new Ellipse { Width = 12, Height = 12, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        swatch.Fill = Palette.FromBambu(job.FilamentColor) is { } color ? Palette.Brush(color) : Brushes.Transparent;
        swatch.SetResourceReference(Shape.StrokeProperty, ThemeKeys.TextTertiary);
        swatch.StrokeThickness = 0.5;
        grid.Children.Add(Ui.At(swatch, 0));

        var when = (job.EndedAt ?? job.StartedAt).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var detail = string.Join(" · ", new[] { printer, job.FilamentMaterial, when }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var text = new Stack { Spacing = 1, Children = { Ui.Text(job.Name, WRamp.Detail, FontWeights.SemiBold), Ui.Text(detail, WRamp.Note, null, ThemeKeys.TextSecondary) } };
        grid.Children.Add(Ui.At(text, 1));

        var outcome = Ui.Text(job.Outcome.Title(), WRamp.Detail, FontWeights.SemiBold, ThemeKeys.TextPrimary);
        outcome.Foreground = Ctx.Ink(OutcomeColor(job.Outcome));
        var length = Ui.Tabular(Ui.Text(job.Duration is { } d ? Format.Duration(d) : "", WRamp.Note, null, ThemeKeys.TextSecondary));
        length.HorizontalAlignment = HorizontalAlignment.Right;
        outcome.HorizontalAlignment = HorizontalAlignment.Right;
        var right = new Stack { Spacing = 1, Margin = new Thickness(12, 0, 0, 0), Children = { outcome, length } };
        grid.Children.Add(Ui.At(right, 2));
        grid.Margin = new Thickness(0, 2, 0, 2);

        var spoken = string.Join(", ", new[] { job.Name, detail, job.Outcome.Title(), length.Text }.Where(s => s.Length > 0));
        return Ui.ReadAsOne(grid, spoken);
    }

    private static Color OutcomeColor(JobOutcome outcome) => outcome switch
    {
        JobOutcome.Finished => Palette.Finished,
        JobOutcome.Failed => Palette.Failed,
        _ => Palette.Idle,
    };

    private static FrameworkElement Dot(Color color) =>
        new Ellipse { Width = 8, Height = 8, Fill = Palette.Brush(color), HorizontalAlignment = HorizontalAlignment.Left };
}
