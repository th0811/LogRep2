namespace FfxiTempLogCollector.Core;

public sealed class CollectorState
{
    public string SessionId { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public Dictionary<string, CollectorFileState> Files { get; set; } = [];

    public HashSet<string> SeenRawRecordIds { get; set; } = [];

    public HashSet<string> SeenCanonicalKeys { get; set; } = [];

    public long LastOrder { get; set; }
}
