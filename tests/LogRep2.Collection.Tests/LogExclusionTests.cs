using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class LogExclusionTests
{
    [Fact]
    public void 除外状態の変更と同数の選択切替で実行可能な操作が変わる()
    {
        using var directory = new TemporaryDirectory();
        var vm = new LogExclusionViewModel([CreateSession(directory.Path)]);
        var first = vm.VisibleRows[0];
        var other = vm.VisibleRows[3];
        AssertOperations(vm, false, false, false, false);
        vm.UpdateSelection([first]);
        AssertOperations(vm, true, false, true, false);
        vm.Apply(LogExclusionOperation.ExcludeRows, [first]);
        AssertOperations(vm, false, true, true, true);
        vm.UpdateSelection([other]);
        AssertOperations(vm, true, false, true, false);
        vm.UpdateSelection([first]);
        vm.Apply(LogExclusionOperation.ExcludeGroups, [first]);
        AssertOperations(vm, false, false, false, true);
        vm.UpdateSelection([first, other]);
        AssertOperations(vm, false, false, true, true);
        vm.Apply(LogExclusionOperation.RestoreGroups, [first, other]);
        AssertOperations(vm, true, false, true, false);
        vm.UpdateSelection([]);
        AssertOperations(vm, false, false, false, false);
    }

    [Fact]
    public void 検索で隠れた行除外をグループ単位で戻せる()
    {
        using var directory = new TemporaryDirectory();
        var vm = new LogExclusionViewModel([CreateSession(directory.Path)]);
        vm.Apply(LogExclusionOperation.ExcludeRows, [vm.VisibleRows[2]]);
        vm.SearchText = "構え";
        vm.SearchCommand.Execute(null);
        vm.UpdateSelection(vm.VisibleRows);
        AssertOperations(vm, true, false, true, true);
        vm.Apply(LogExclusionOperation.RestoreGroups, vm.VisibleRows);
        Assert.False(vm.CanRestoreGroups);
        Assert.Equal(0, vm.ExcludedCount);
        vm.SearchText = "存在しないログ";
        vm.SearchCommand.Execute(null);
        AssertOperations(vm, false, false, false, false);
    }

    [Fact]
    public void 混在選択は変更可能な行がある場合に有効で不正なIDを含む操作は無効()
    {
        using var directory = new TemporaryDirectory();
        var vm = new LogExclusionViewModel([CreateSession(directory.Path)]);
        vm.Apply(LogExclusionOperation.ExcludeRows, [vm.VisibleRows[0]]);
        vm.UpdateSelection(vm.VisibleRows.Take(2));
        AssertOperations(vm, true, true, true, true);
        var invalid = CreateSession(directory.Path, [new() { SessionId = "session", EventGroup = "g1" }]);
        vm.SelectedSession = invalid;
        AssertOperations(vm, false, false, false, false);
        vm.UpdateSelection(vm.VisibleRows);
        AssertOperations(vm, false, false, true, false);
        vm.SelectedSession = CreateSession(directory.Path, [new() { SessionId = "session", CanonicalRecordId = "1" }]);
        vm.UpdateSelection(vm.VisibleRows);
        AssertOperations(vm, true, false, false, false);
    }

    private static void AssertOperations(LogExclusionViewModel vm, bool excludeRows, bool restoreRows, bool excludeGroups, bool restoreGroups)
    {
        Assert.Equal(excludeRows, vm.CanExcludeRows);
        Assert.Equal(restoreRows, vm.CanRestoreRows);
        Assert.Equal(excludeGroups, vm.CanExcludeGroups);
        Assert.Equal(restoreGroups, vm.CanRestoreGroups);
    }

    [Fact]
    public void 選択状態と除外件数が操作バーと絞り込みに反映される()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new LogExclusionViewModel([CreateSession(directory.Path)]);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        Assert.False(viewModel.CanApply);
        Assert.True(viewModel.ShowAllRows);

        viewModel.UpdateSelection(viewModel.VisibleRows.Take(2));
        Assert.True(viewModel.CanApply);
        Assert.Equal("2行を選択中", viewModel.SelectionSummaryText);
        Assert.Contains(nameof(viewModel.CanApply), notifications);
        viewModel.Apply(LogExclusionOperation.ExcludeGroups, [viewModel.VisibleRows[0]]);
        Assert.Equal(3, viewModel.ExcludedCount);
        Assert.Contains(nameof(viewModel.ExcludedCount), notifications);

        viewModel.ExcludedOnly = true;
        Assert.False(viewModel.ShowAllRows);
        Assert.Equal(3, viewModel.VisibleRows.Count);
        Assert.Contains(nameof(viewModel.ShowAllRows), notifications);
        viewModel.ShowAllRows = true;
        Assert.Equal(5, viewModel.VisibleRows.Count);
        viewModel.UpdateSelection([]);
        Assert.False(viewModel.CanApply);
        Assert.Contains("Ctrl / Shift", viewModel.SelectionSummaryText);
    }

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
        viewModel.UpdateSelection(viewModel.VisibleRows.Take(1));
        Assert.False(viewModel.CanApply);
        viewModel.Apply(LogExclusionOperation.ExcludeRows, [viewModel.VisibleRows[0]]);
        AssertOperations(viewModel, false, false, false, false);
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
