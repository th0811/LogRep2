using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class PartyMemberManagerViewModelTests
{
    [Fact]
    public void 初期表示で先頭候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            Candidates("Charlie", "Bob"),
            _ => { });

        Assert.Equal("Bob", viewModel.SelectedCandidate?.Name);
    }

    [Fact]
    public void 候補追加と並べ替えを保存する()
    {
        IReadOnlyList<string> saved = [];
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            Candidates("Bob"),
            members => saved = members);

        Select(viewModel, "Bob");
        viewModel.AddCandidateCommand.Execute(null);
        viewModel.SelectedMember = "Bob";
        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal(["Bob", "Alice"], saved);
        Assert.Equal("現在のPTメンバー（2 / 6）", viewModel.CountText);
    }

    [Fact]
    public void PTメンバーは最大6名に制限する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["A", "B", "C", "D", "E", "F"],
            Candidates("G"),
            _ => { });

        Select(viewModel, "G");

        Assert.False(viewModel.AddCandidateCommand.CanExecute(null));
    }

    [Fact]
    public void 候補追加後は同じ位置の次候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            Candidates("Bob", "Charlie", "Dave"),
            _ => { });
        Select(viewModel, "Charlie");

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal("Dave", viewModel.SelectedCandidate?.Name);
    }

    [Fact]
    public void 末尾候補追加後は一つ前の候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            Candidates("Bob", "Charlie"),
            _ => { });
        Select(viewModel, "Charlie");

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal("Bob", viewModel.SelectedCandidate?.Name);
    }

    [Fact]
    public void メンバー削除後は同じ位置の次メンバーを選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob", "Charlie"],
            [],
            _ => { });
        viewModel.SelectedMember = "Bob";

        viewModel.RemoveCommand.Execute(null);

        Assert.Equal("Charlie", viewModel.SelectedMember);
    }

    [Fact]
    public void 末尾メンバー削除後は一つ前のメンバーを選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob"],
            [],
            _ => { });
        viewModel.SelectedMember = "Bob";

        viewModel.RemoveCommand.Execute(null);

        Assert.Equal("Alice", viewModel.SelectedMember);
    }

    [Fact]
    public void 候補追加で上限到達時は候補選択を解除する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["A", "B", "C", "D", "E"],
            Candidates("F", "G"),
            _ => { });

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Null(viewModel.SelectedCandidate);
        Assert.False(viewModel.AddCandidateCommand.CanExecute(null));
    }

    [Fact]
    public void 候補の出現数を書式化する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            [],
            [new PartyMemberCandidate("Alice", 1284), new PartyMemberCandidate("Bob", 0)],
            _ => { });

        Assert.Equal("1,284行", viewModel.Candidates[0].OccurrenceText);
        Assert.Equal(string.Empty, viewModel.Candidates[1].OccurrenceText);
    }

    [Fact]
    public void 登録メンバーから外した候補は出現数を保つ()
    {
        var viewModel = new PartyMemberManagerViewModel(
            [],
            [new PartyMemberCandidate("Alice", 1284)],
            _ => { });
        Select(viewModel, "Alice");
        viewModel.AddCandidateCommand.Execute(null);

        viewModel.SelectedMember = "Alice";
        viewModel.RemoveCommand.Execute(null);

        Assert.Equal("1,284行", viewModel.Candidates.Single().OccurrenceText);
    }

    [Fact]
    public void 人数上限を進捗バー向けに公開する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob"],
            [],
            _ => { });

        Assert.Equal(2, viewModel.MemberCount);
        Assert.Equal(6, viewModel.MemberCapacity);
    }

    private static PartyMemberCandidate[] Candidates(params string[] names) =>
        [.. names.Select(name => new PartyMemberCandidate(name, 0))];

    private static void Select(
        PartyMemberManagerViewModel viewModel,
        string name) =>
        viewModel.SelectedCandidate = viewModel.Candidates
            .Single(candidate => candidate.Name == name);
}
