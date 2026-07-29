namespace FfxiTempLogCollector.App;

/// <summary>
/// PTメンバー設定画面に表示するPC候補。
/// ログ内での出現数（そのPCが関与したログ行数）を併記するために名前だけでなく件数を保持する。
/// </summary>
public sealed class PartyMemberCandidate(string name, int occurrenceCount)
{
    public string Name { get; } = name
        ?? throw new ArgumentNullException(nameof(name));

    public int OccurrenceCount { get; } = occurrenceCount;

    /// <summary>
    /// 一覧の右端に出す出現数。0件のときは何も表示しない。
    /// </summary>
    public string OccurrenceText => OccurrenceCount <= 0
        ? string.Empty
        : $"{OccurrenceCount:N0}行";

    public override string ToString() => Name;
}
