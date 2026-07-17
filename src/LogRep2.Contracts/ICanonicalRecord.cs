namespace LogRep2.Contracts;

/// <summary>
/// 収集処理と分析処理の間で共有するcanonicalレコードの読み取り専用境界です。
/// </summary>
public interface ICanonicalRecord
{
    string? CanonicalRecordId { get; }
    string? SessionId { get; }
    long? Order { get; }
    DateTimeOffset? FirstSeenAt { get; }
    DateTimeOffset? LastSeenAt { get; }
    string? EventGroup { get; }
    long? SequenceHintMin { get; }
    long? SequenceHintMax { get; }
    string? VisibleText { get; }
    string? MessageTimeText { get; }
    bool IsMarker { get; }
    string? MarkerKeyword { get; }
}
