namespace FfxiTempLogCollector.App;

/// <summary>
/// 稼働状況ピルの配色。Theme/Styles.xaml のデザイントークンと同じ値を使う。
/// </summary>
public sealed record StatusPalette(
    string Foreground,
    string Background,
    string Border,
    string Dot)
{
    /// <summary>停止中など、状態を主張しないとき。</summary>
    public static StatusPalette Neutral { get; } =
        new("#475467", "#F9FAFB", "#E4E8ED", "#98A2B3");

    /// <summary>稼働中。</summary>
    public static StatusPalette Success { get; } =
        new("#067647", "#ECFDF3", "#ABEFC6", "#12B76A");

    /// <summary>開始・停止処理中など、遷移中。</summary>
    public static StatusPalette Warning { get; } =
        new("#B54708", "#FFFAEB", "#FEDF89", "#F79009");

    /// <summary>エラー。</summary>
    public static StatusPalette Danger { get; } =
        new("#B42318", "#FEF3F2", "#FECDCA", "#F04438");
}
