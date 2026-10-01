using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class PartyMemberManagerViewModelTests
{
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

    [Theory]
    [InlineData("Charlie", "Dave")]
    [InlineData("Dave", "Charlie")]
    public void 候補追加後は次候補または末尾候補を選択する(string selected, string expected)
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"], Candidates("Bob", "Charlie", "Dave"), _ => { });
        Select(viewModel, selected);

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal(expected, viewModel.SelectedCandidate?.Name);
    }

    [Theory]
    [InlineData("Bob", "Charlie")]
    [InlineData("Charlie", "Bob")]
    public void メンバー削除後は次メンバーまたは末尾メンバーを選択する(string selected, string expected)
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob", "Charlie"], [], _ => { });
        viewModel.SelectedMember = selected;

        viewModel.RemoveCommand.Execute(null);

        Assert.Equal(expected, viewModel.SelectedMember);
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

    private static PartyMemberCandidate[] Candidates(params string[] names) =>
        [.. names.Select(name => new PartyMemberCandidate(name, 0))];

    private static void Select(
        PartyMemberManagerViewModel viewModel,
        string name) =>
        viewModel.SelectedCandidate = viewModel.Candidates
            .Single(candidate => candidate.Name == name);
}
