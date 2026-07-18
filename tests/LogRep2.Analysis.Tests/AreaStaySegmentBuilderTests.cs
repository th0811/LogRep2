using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class AreaStaySegmentBuilderTests
{
    [Fact]
    public void Build_同名エリアを訪問回数とログ位置で別区間にする()
    {
        var records = new[]
        {
            CreateRecord(1, "=== 西ロンフォール ==="),
            CreateRecord(2, "戦闘ログ", "[21:05]"),
            CreateRecord(3, "=== 南サンドリア ==="),
            CreateRecord(4, "移動ログ"),
            CreateRecord(5, "=== 西ロンフォール ==="),
            CreateRecord(6, "戦闘ログ2", "[22:18]"),
        };

        var segments = new AreaStaySegmentBuilder().Build(records);

        Assert.Equal(3, segments.Count);
        Assert.Equal(("西ロンフォール", 1, 1, 1, 3, "[21:05]"),
            ToValues(segments[0]));
        Assert.Equal(("南サンドリア", 1, 2, 1, 5, null),
            ToValues(segments[1]));
        Assert.Equal(("西ロンフォール", 2, 3, 1, null, "[22:18]"),
            ToValues(segments[2]));
    }

    [Fact]
    public void CreateSelection_エリア行の直後から次のエリア行の直前を選択する()
    {
        var records = new[]
        {
            CreateRecord(10, "=== エリアA ==="),
            CreateRecord(11, "対象1"),
            CreateRecord(12, "対象2"),
            CreateRecord(20, "=== エリアB ==="),
            CreateRecord(21, "対象外"),
        };
        var segment = new AreaStaySegmentBuilder().Build(records)[0];

        var range = new AnalysisRangeBuilder().Build(
            records,
            segment.CreateSelection());

        Assert.Equal(["対象1", "対象2"], range.Select(record => record.VisibleText));
    }

    [Theory]
    [InlineData("=== ジュノ港 ===", true)]
    [InlineData(" ===  ジュノ港  === ", true)]
    [InlineData("== ジュノ港 ==", false)]
    [InlineData("=== ===", false)]
    public void IsAreaChange_形式が一致する行だけを認識する(string text, bool expected)
    {
        Assert.Equal(expected, AreaChangeExtractor.IsAreaChange(text));
    }

    private static (string, int, int, int, long?, string?) ToValues(AreaStaySegment segment) =>
        (segment.AreaName, segment.AreaOccurrence, segment.Sequence,
            segment.RecordCount, segment.End?.Order, segment.FirstMessageTimeText);

    private static CanonicalRecord CreateRecord(
        long order,
        string visibleText,
        string? messageTimeText = null) =>
        new()
        {
            Order = order,
            VisibleText = visibleText,
            MessageTimeText = messageTimeText,
        };
}
