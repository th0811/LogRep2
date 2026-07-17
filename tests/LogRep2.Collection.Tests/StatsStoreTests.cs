using System.Text.Json;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class StatsStoreTests
{
    [Fact]
    public void StatsJsonを保存できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new StatsStore();
        var stats = new CollectorStats
        {
            RawRecordsWritten = 10,
            CanonicalRecordsWritten = 8,
            DuplicateRawRecordsSkipped = 2,
            DuplicateCanonicalRecordsSkipped = 1,
            ParseErrors = 3,
            DecodeErrors = 4,
            GapWarnings = 5,
            LastSeenAt = new DateTimeOffset(
                2026,
                6,
                23,
                21,
                40,
                0,
                TimeSpan.FromHours(9)),
        };

        store.Save(temporaryDirectory.Path, stats);

        var statsPath = temporaryDirectory.GetPath("stats.json");
        Assert.True(File.Exists(statsPath));

        using var document = JsonDocument.Parse(File.ReadAllText(statsPath));
        var root = document.RootElement;
        Assert.Equal(10, root.GetProperty("raw_records_written").GetInt64());
        Assert.Equal(8, root.GetProperty("canonical_records_written").GetInt64());
        Assert.Equal(5, root.GetProperty("gap_warnings").GetInt64());

        var reloaded = store.Load(temporaryDirectory.Path);
        Assert.Equal(stats.DecodeErrors, reloaded.DecodeErrors);
        Assert.Equal(stats.LastSeenAt, reloaded.LastSeenAt);
    }
}
