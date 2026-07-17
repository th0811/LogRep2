namespace FfxiTempLogCollector.Core;

public sealed class StatsStore
{
    public const string StatsFileName = "stats.json";

    public CollectorStats Load(string sessionDirectory)
    {
        return JsonFileSerializer.Load<CollectorStats>(
            GetStatsPath(sessionDirectory));
    }

    public void Save(string sessionDirectory, CollectorStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        JsonFileSerializer.Save(GetStatsPath(sessionDirectory), stats);
    }

    private static string GetStatsPath(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

        return Path.Combine(sessionDirectory, StatsFileName);
    }
}
