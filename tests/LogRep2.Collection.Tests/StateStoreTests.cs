using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class StateStoreTests
{
    [Fact]
    public void StateJsonを保存して読み込める()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new StateStore();
        var updatedAt = new DateTimeOffset(
            2026,
            6,
            23,
            21,
            35,
            0,
            TimeSpan.FromHours(9));
        var expected = new CollectorState
        {
            SessionId = "20260623-213000",
            UpdatedAt = updatedAt,
            Files =
            {
                ["1_0.log"] = new CollectorFileState
                {
                    Exists = true,
                    FileSize = 1234,
                    FileHash = "abc123",
                },
            },
            SeenRawRecordIds = ["raw-1"],
            SeenCanonicalKeys = ["canonical-1"],
            LastOrder = 42,
        };

        store.Save(temporaryDirectory.Path, expected);
        var actual = store.Load(temporaryDirectory.Path);

        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
        Assert.True(actual.Files["1_0.log"].Exists);
        Assert.Equal(1234, actual.Files["1_0.log"].FileSize);
        Assert.Contains("raw-1", actual.SeenRawRecordIds);
        Assert.Contains("canonical-1", actual.SeenCanonicalKeys);
        Assert.Equal(42, actual.LastOrder);
    }
}
