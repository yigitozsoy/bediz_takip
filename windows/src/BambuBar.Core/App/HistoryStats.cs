using BambuBar.Core.Model;

namespace BambuBar.Core.App;

/// <summary>PrintPulse's History page: totals over the prints the app saw end (running ones don't count yet).</summary>
public sealed record HistoryStats
{
    public int Total { get; init; }
    public int Finished { get; init; }
    public int Failed { get; init; }
    public int Canceled { get; init; }
    /// <summary>Ended while the app wasn't running, so how isn't known.</summary>
    public int Unknown { get; init; }
    /// <summary>Time spent printing: every ended print with a known length.</summary>
    public TimeSpan PrintTime { get; init; }
    public JobRecord? Longest { get; init; }
    /// <summary>Prints that ended in the last 7 days.</summary>
    public int LastWeek { get; init; }
    /// <summary>Materials by how many prints used them, most first.</summary>
    public IReadOnlyList<(string Material, int Count)> Materials { get; init; } = [];

    /// <summary>Finished out of the prints whose end is known; null when there are none.</summary>
    public double? SuccessRate => Finished + Failed + Canceled is var known && known > 0 ? (double)Finished / known : null;

    public static HistoryStats Of(IEnumerable<JobRecord> jobs, DateTimeOffset now)
    {
        var ended = jobs.Where(j => j.Outcome != JobOutcome.Running).ToList();
        var timed = ended.Where(j => j.Duration is { } d && d > TimeSpan.Zero).ToList();
        return new HistoryStats
        {
            Total = ended.Count,
            Finished = ended.Count(j => j.Outcome == JobOutcome.Finished),
            Failed = ended.Count(j => j.Outcome == JobOutcome.Failed),
            Canceled = ended.Count(j => j.Outcome == JobOutcome.Canceled),
            Unknown = ended.Count(j => j.Outcome == JobOutcome.Ended),
            PrintTime = timed.Aggregate(TimeSpan.Zero, (sum, j) => sum + j.Duration!.Value),
            Longest = timed.MaxBy(j => j.Duration!.Value),
            LastWeek = ended.Count(j => (j.EndedAt ?? j.StartedAt) >= now.AddDays(-7)),
            Materials = ended.Where(j => !string.IsNullOrWhiteSpace(j.FilamentMaterial))
                .GroupBy(j => j.FilamentMaterial!.Trim().ToUpperInvariant())
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(m => m.Item2).ThenBy(m => m.Key, StringComparer.Ordinal)
                .ToList(),
        };
    }
}
