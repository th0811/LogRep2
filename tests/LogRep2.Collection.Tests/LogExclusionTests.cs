using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class LogExclusionTests
{
    [Fact]
    public void 構えの検索から非表示のダメージ行もグループ除外して復元できる()
    {
        using var directory = new TemporaryDirectory();
        var session = CreateSession(directory.Path);
        var viewModel = new LogExclusionViewModel([session]) { SearchText = "トアクリーバの構え" };
        viewModel.SearchCommand.Execute(null);
        var match = Assert.Single(viewModel.VisibleRows);
        Assert.Contains(viewModel.ContextRows, row => row.Text.Contains("10ダメージ"));
        viewModel.ShowGroupOnly = true;
        Assert.Equal(3, viewModel.ContextRows.Count);

        viewModel.Apply(LogExclusionOperation.ExcludeGroups, [match]);

        Assert.True(viewModel.HasChanges);
        Assert.All(viewModel.ContextRows, row => Assert.True(row.IsExcluded));
        var restored = CreateSession(directory.Path);
        restored.LoadExclusions();
        Assert.Equal(3, restored.Records.Count(restored.Exclusions.IsExcluded));
        Assert.False(restored.Exclusions.IsExcluded(Record("other", "1", "g1", "別セッション", 1)));
        Assert.True(restored.Exclusions.IsExcluded(Record("session", "new", "g1", "追記された行", 6)));

        viewModel.Apply(LogExclusionOperation.RestoreRows, [match]);
        Assert.Contains("グループ全体", viewModel.StatusMessage);
        Assert.True(match.IsExcluded);
        viewModel.Apply(LogExclusionOperation.RestoreGroups, [match]);
        Assert.Empty(new AnalysisExclusionsStore().Load(directory.Path).Groups);
        Assert.All(viewModel.ContextRows, row => Assert.False(row.IsExcluded));
    }

    [Fact]
    public void 行除外と複数グループ操作と除外済み検索ができる()
    {
        using var directory = new TemporaryDirectory();
        var session = CreateSession(directory.Path);
        var viewModel = new LogExclusionViewModel([session]);
        var damage = viewModel.VisibleRows[2];
        viewModel.Apply(LogExclusionOperation.ExcludeRows, [damage]);
        Assert.True(damage.IsExcluded);
        Assert.False(viewModel.VisibleRows[0].IsExcluded);
        viewModel.ExcludedOnly = true;
        Assert.Same(damage, Assert.Single(viewModel.VisibleRows));
        viewModel.Apply(LogExclusionOperation.RestoreRows, [damage]);
        Assert.Empty(viewModel.VisibleRows);
        viewModel.ExcludedOnly = false;
        viewModel.Apply(LogExclusionOperation.ExcludeGroups, [viewModel.VisibleRows[0], viewModel.VisibleRows[3]]);
        Assert.All(viewModel.VisibleRows, row => Assert.True(row.IsExcluded));
        viewModel.Apply(LogExclusionOperation.RestoreGroups, [viewModel.VisibleRows[0], viewModel.VisibleRows[3]]);
        Assert.All(viewModel.VisibleRows, row => Assert.False(row.IsExcluded));
    }

    [Fact]
    public void 保存失敗では画面も保存済みの除外状態も変更しない()
    {
        using var directory = new TemporaryDirectory();
        var session = CreateSession(directory.Path);
        session.SaveExclusions(new AnalysisExclusions());
        var viewModel = new LogExclusionViewModel([session]);
        using (File.Open(Path.Combine(directory.Path, AnalysisExclusionsStore.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            viewModel.Apply(LogExclusionOperation.ExcludeRows, [viewModel.VisibleRows[0]]);
        }

        Assert.False(viewModel.HasChanges);
        Assert.False(viewModel.VisibleRows[0].IsExcluded);
        Assert.Contains("変更を適用できません", viewModel.StatusMessage);
        Assert.Empty(new AnalysisExclusionsStore().Load(directory.Path).Records);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Theory]
    [InlineData("不正なJSON")]
    [InlineData("{\"schema_version\":2}")]
    [InlineData("{\"records\":null}")]
    [InlineData("{\"groups\":[null]}")]
    public void 不正な除外ファイルを空として上書きしない(string json)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, AnalysisExclusionsStore.FileName);
        File.WriteAllText(path, json);
        var session = CreateSession(directory.Path);
        session.LoadExclusions();
        var viewModel = new LogExclusionViewModel([session]);
        Assert.NotNull(session.ExclusionsLoadError);
        Assert.False(viewModel.CanEdit);
        viewModel.Apply(LogExclusionOperation.ExcludeRows, [viewModel.VisibleRows[0]]);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void 行IDなしは行除外できずグループIDなしはグループ除外できない()
    {
        using var directory = new TemporaryDirectory();
        var session = CreateSession(directory.Path, [new() { SessionId = "session", VisibleText = "IDなし" }]);
        var viewModel = new LogExclusionViewModel([session]);
        viewModel.Apply(LogExclusionOperation.ExcludeRows, viewModel.VisibleRows);
        Assert.Contains("行ID", viewModel.StatusMessage);
        viewModel.Apply(LogExclusionOperation.ExcludeGroups, viewModel.VisibleRows);
        Assert.Contains("グループID", viewModel.StatusMessage);
        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void セッション切替と検索結果の前後移動ができる()
    {
        using var directory = new TemporaryDirectory();
        var first = CreateSession(directory.Path);
        var second = CreateSession(directory.Path, [Record("other", "x", "g1", "他のログ", 1)]);
        var viewModel = new LogExclusionViewModel([first, second]) { SearchText = "ダメージ" };
        viewModel.SearchCommand.Execute(null);
        Assert.Equal(2, viewModel.VisibleRows.Count);
        var initial = viewModel.SelectedRow;
        viewModel.NextMatchCommand.Execute(null);
        Assert.NotSame(initial, viewModel.SelectedRow);
        viewModel.PreviousMatchCommand.Execute(null);
        Assert.Same(initial, viewModel.SelectedRow);
        viewModel.SelectedSession = second;
        Assert.Empty(viewModel.VisibleRows);
        Assert.Empty(viewModel.ContextRows);
        viewModel.SearchText = string.Empty;
        viewModel.SearchCommand.Execute(null);
        Assert.Equal("他のログ", Assert.Single(viewModel.VisibleRows).Text);
    }

    internal static SessionSelectionViewModel CreateSession(string folder, IReadOnlyList<CanonicalRecord>? records = null) =>
        new(new AnalyzerInputSession(folder, "", "", "", null,
            new SessionInfo { SessionId = "session" }, new StatsInfo()), records ?? CreateRecords(), []);

    internal static CanonicalRecord[] CreateRecords() =>
    [
        Record("session", "1", "g1", "Xitraは、トアクリーバの構え。", 1),
        Record("session", "2", "g1", "Xitraは、トアクリーバを実行。", 2),
        Record("session", "3", "g1", "→Goblinに、10ダメージ。", 3),
        Record("session", "4", "g2", "Xitraは、トアクリーバを実行。", 4),
        Record("session", "5", "g2", "→Goblinに、100ダメージ。", 5),
    ];

    private static CanonicalRecord Record(string session, string id, string group, string text, long order) => new()
    {
        SessionId = session,
        CanonicalRecordId = id,
        EventGroup = group,
        VisibleText = text,
        Order = order,
        MessageTimeText = order == 1 ? "[10:00:00]" : "[10:00:20]",
    };
}
