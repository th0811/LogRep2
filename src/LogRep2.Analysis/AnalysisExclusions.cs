using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed record ExcludedRecordKey(string SessionId, string CanonicalRecordId);

public sealed class AnalysisExclusions
{
    public int SchemaVersion { get; set; } = 1;

    public HashSet<ExcludedRecordKey> Records { get; set; } = [];

    public HashSet<ActionGroupKey> Groups { get; set; } = [];

    public bool IsRecordExcluded(ICanonicalRecord record) =>
        TryGetRecordKey(record) is { } key && Records.Contains(key);

    public bool IsGroupExcluded(ICanonicalRecord record) =>
        TryGetGroupKey(record) is { } key && Groups.Contains(key);

    public bool IsExcluded(ICanonicalRecord record) =>
        IsRecordExcluded(record) || IsGroupExcluded(record);

    public AnalysisExclusions Clone() => new()
    {
        Records = [.. Records],
        Groups = [.. Groups],
    };

    public static ExcludedRecordKey? TryGetRecordKey(ICanonicalRecord record) =>
        string.IsNullOrWhiteSpace(record.SessionId) || string.IsNullOrWhiteSpace(record.CanonicalRecordId)
            ? null
            : new(record.SessionId, record.CanonicalRecordId);

    public static ActionGroupKey? TryGetGroupKey(ICanonicalRecord record) =>
        string.IsNullOrWhiteSpace(record.SessionId) || string.IsNullOrWhiteSpace(record.EventGroup)
            ? null
            : new(record.SessionId, record.EventGroup);
}
