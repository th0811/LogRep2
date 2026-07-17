using LogRep2.Contracts;

namespace FfxiTempLogCollector.Core;

public sealed record CanonicalSnapshot(
    string SessionId,
    IReadOnlyList<ICanonicalRecord> Records,
    DateTimeOffset UpdatedAt);
