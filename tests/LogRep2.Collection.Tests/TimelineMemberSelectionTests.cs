using FFXI_LogAnalyzer.App;

namespace FfxiTempLogCollector.Tests;

public sealed class TimelineMemberSelectionTests
{
    [Fact]
    public void 候補更新で残るPCの選択を維持し消えたPCと新規PCは選択しない()
    {
        (string Name, string Classification)[] candidates = [("Alice", "PC候補"), ("Boro", "PC登録")];
        var viewModel = new TimelineMemberSelectionViewModel(() => candidates);
        viewModel.SelectWhere(_ => true);
        candidates = [("Alice", "PC登録"), ("Carol", "PC候補")];
        viewModel.Refresh();
        Assert.Equal("Alice", Assert.Single(viewModel.SelectedMembers));
        candidates = [("Alice", "PC候補"), ("Boro", "PC登録"), ("Carol", "PC候補")];
        viewModel.Refresh();
        Assert.Equal("Alice", Assert.Single(viewModel.SelectedMembers));
        viewModel.SelectWhere(kind => kind == "PC登録");
        Assert.Equal("Boro", Assert.Single(viewModel.SelectedMembers));
        viewModel.SelectWhere(_ => false);
        Assert.Empty(viewModel.SelectedMembers);
    }

    [Fact]
    public void 候補ゼロから登録修正後に候補を表示でき一覧外は選択できない()
    {
        (string Name, string Classification)[] candidates = [];
        var viewModel = new TimelineMemberSelectionViewModel(() => candidates);
        viewModel.SetSelected("Alice", true);
        Assert.Empty(viewModel.SelectedMembers);
        candidates = [("Alice", "PC登録")];
        viewModel.Refresh();
        Assert.Single(viewModel.Candidates);
        Assert.Empty(viewModel.SelectedMembers);
        viewModel.SetSelected("Alice", true);
        Assert.Equal("Alice", Assert.Single(viewModel.SelectedMembers));
        candidates = [];
        viewModel.Refresh();
        Assert.Empty(viewModel.SelectedMembers);
    }
}
