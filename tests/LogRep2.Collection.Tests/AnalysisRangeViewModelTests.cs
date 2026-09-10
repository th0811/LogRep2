using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace LogRep2.Collection.Tests;

public sealed class AnalysisRangeViewModelTests
{
    [Fact]
    public void 初期モードはエリアログ区間になる()
    {
        var viewModel = new AnalysisRangeViewModel();

        Assert.True(viewModel.IsAreaSegmentMode);
        Assert.False(viewModel.IsManualRangeMode);
    }

    [Fact]
    public async Task ログ読込時にエリアログ区間へ戻して選択肢を表示する()
    {
        var viewModel = new AnalysisRangeViewModel
        {
            IsManualRangeMode = true,
        };
        var records = new CanonicalRecord[]
        {
            new() { Order = 1, VisibleText = "=== 西ロンフォール ===" },
            new() { Order = 2, VisibleText = "戦闘ログ" },
        };

        await viewModel.LoadRecordsAsync(
            records,
            CancellationToken.None);

        Assert.True(viewModel.IsAreaSegmentMode);
        Assert.False(viewModel.IsManualRangeMode);
        Assert.True(viewModel.HasAreaSegments);
        Assert.Single(viewModel.FilteredAreaSegments);
        Assert.Null(viewModel.SelectedAreaSegment);
    }
}
