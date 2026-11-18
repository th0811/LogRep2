using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;
using System.Text.Json;

namespace FfxiTempLogCollector.Tests;

public sealed class SavedAnalysisRangeTests
{
    [Fact]
    public void ログ行境界を含み従来のマーカー境界は含めない()
    {
        var records = LogExclusionTests.CreateRecords();
        var range = new AnalysisRangeSelection(AnalysisEndpoint.FromRecord(records[0]), AnalysisEndpoint.FromRecord(records[2]));
        Assert.Equal(records.Take(3), new AnalysisRangeBuilder().Build(records, range));
        var single = range with { End = range.Start };
        Assert.Single(new AnalysisRangeBuilder().Build(records, single));
        var invalid = range with { Start = range.End, End = range.Start };
        Assert.Throws<ArgumentException>(() => new AnalysisRangeBuilder().Build(records, invalid));
        Assert.False(new AnalysisRangeValidator().IsValid(range with { End = AnalysisEndpoint.LogEnd }));
        var start = new MarkerRecord(records[0].Order, "", "", "", null, records[0]);
        var end = new MarkerRecord(records[2].Order, "", "", "", null, records[2]);
        Assert.Same(records[1], Assert.Single(new AnalysisRangeBuilder().Build(records,
            new(AnalysisEndpoint.FromMarker(start), AnalysisEndpoint.FromMarker(end)))));
        Assert.Equal(20, new AnalysisTimeResolver().Resolve(range, []).DurationSeconds);
    }

    [Fact]
    public void 検索非表示の中間行を含めグループ境界を明示的に広げられる()
    {
        using var directory = new TemporaryDirectory();
        var vm = new LogRangeViewModel([LogExclusionTests.CreateSession(directory.Path)]);
        vm.SearchText = "10ダメージ";
        vm.SearchCommand.Execute(null);
        Assert.Single(vm.VisibleRows);
        vm.SetStartCommand.Execute(null);
        vm.SetEndCommand.Execute(null);
        Assert.True(vm.CanUseRange);
        Assert.NotEmpty(vm.GroupWarning);
        vm.AlignGroupsCommand.Execute(null);
        Assert.Equal(1, vm.StartRow!.Order);
        Assert.Equal(3, vm.EndRow!.Order);
        Assert.Empty(vm.GroupWarning);
        Assert.Contains("3行", vm.RangeSummary);
        Assert.Single(vm.VisibleRows);
        vm.IncludeMatchingGroups = true;
        Assert.Equal(3, vm.VisibleRows.Count);
        Assert.All(vm.VisibleRows, row => Assert.True(row.IsInRange));
        Assert.Equal("開始", vm.VisibleRows[0].Boundary);
        Assert.Equal("終了", vm.VisibleRows[2].Boundary);
    }

    [Fact]
    public void 保存区間の再読込と削除で元ログや他の設定を変更しない()
    {
        using var directory = new TemporaryDirectory();
        var session = LogExclusionTests.CreateSession(directory.Path);
        File.WriteAllText(directory.GetPath("canonical_records.jsonl"), "元ログ");
        session.SaveAlias("装備検証");
        var alias = File.ReadAllText(directory.GetPath(SessionAnnotationsStore.FileName));
        var vm = new LogRangeViewModel([session]);
        Assert.Empty(vm.SavedRanges);
        Assert.False(File.Exists(directory.GetPath(SavedAnalysisRangesStore.FileName)));
        SetRange(vm, 0, 2);
        vm.RangeName = "ボス戦・装備Ａ";
        vm.SaveRangeCommand.Execute(null);
        Assert.Single(vm.SavedRanges);
        var reopened = new LogRangeViewModel([session]);
        reopened.SelectedSavedRange = Assert.Single(reopened.SavedRanges);
        Assert.Equal(1, reopened.StartRow!.Order);
        Assert.Equal(3, reopened.EndRow!.Order);
        Assert.True(reopened.CanUseRange);
        reopened.DeleteSelectedRange();
        Assert.Empty(new SavedAnalysisRangesStore().Load(directory.Path, session.SessionId).Ranges);
        Assert.Equal("元ログ", File.ReadAllText(directory.GetPath("canonical_records.jsonl")));
        Assert.Equal(alias, File.ReadAllText(directory.GetPath(SessionAnnotationsStore.FileName)));
    }

    [Fact]
    public void 保存失敗では名前と境界を保持し既存ファイルを壊さない()
    {
        using var directory = new TemporaryDirectory();
        var vm = new LogRangeViewModel([LogExclusionTests.CreateSession(directory.Path)]);
        SetRange(vm, 0, 2);
        vm.RangeName = "初回";
        vm.SaveRangeCommand.Execute(null);
        var path = directory.GetPath(SavedAnalysisRangesStore.FileName);
        var original = File.ReadAllText(path);
        vm.RangeName = "再試行する名前";
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            vm.SaveRangeCommand.Execute(null);
        Assert.Contains("保存できません", vm.StatusMessage);
        Assert.Equal("再試行する名前", vm.RangeName);
        Assert.True(vm.CanUseRange);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Single(vm.SavedRanges);
        vm.SaveRangeCommand.Execute(null);
        Assert.Equal(2, vm.SavedRanges.Count);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Theory]
    [InlineData("不正")]
    [InlineData("""{"schema_version":2,"session_id":"session","ranges":[]}""")]
    [InlineData("""{"schema_version":1,"session_id":"other","ranges":[]}""")]
    [InlineData("""{"schema_version":1,"session_id":"session","ranges":null}""")]
    public void 読込失敗は保存削除を無効にし一時区間は利用できる(string json)
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.GetPath(SavedAnalysisRangesStore.FileName), json);
        var vm = new LogRangeViewModel([LogExclusionTests.CreateSession(directory.Path)]);
        SetRange(vm, 0, 2);
        vm.RangeName = "上書き禁止";
        Assert.True(vm.UseRangeCommand.CanExecute(null));
        Assert.False(vm.SaveRangeCommand.CanExecute(null));
        Assert.False(vm.DeleteRangeCommand.CanExecute(null));
        vm.SaveRangeCommand.Execute(null);
        Assert.Equal(json, File.ReadAllText(directory.GetPath(SavedAnalysisRangesStore.FileName)));
    }

    [Fact]
    public void 行ID欠落の旧ログは一時区間だけ使え欠落や重複した保存境界を誤復元しない()
    {
        using var directory = new TemporaryDirectory();
        var records = LogExclusionTests.CreateRecords();
        var vm = new LogRangeViewModel([LogExclusionTests.CreateSession(directory.Path,
            [new() { Order = 1, VisibleText = "旧ログ" }])]);
        SetRange(vm, 0, 0);
        vm.RangeName = "旧ログ";
        Assert.True(vm.CanUseRange);
        Assert.False(vm.CanSaveRange);
        var saved = new SavedAnalysisRange("id", "名前", "1", "3");
        Assert.Throws<InvalidDataException>(() => SavedAnalysisRangesStore.Resolve(records.Skip(1).ToArray(), "session", saved));
        Assert.Throws<InvalidDataException>(() => SavedAnalysisRangesStore.Resolve(records.Concat([records[0]]).ToArray(), "session", saved));
        new SavedAnalysisRangesStore().Save(directory.Path, new() { SessionId = "session", Ranges = [saved] });
        vm = new LogRangeViewModel([LogExclusionTests.CreateSession(directory.Path, records.Skip(1).ToArray())]);
        vm.SelectedSavedRange = vm.SavedRanges[0];
        Assert.False(vm.CanUseRange);
        Assert.Contains("見つからない", vm.StatusMessage);
    }

    [Fact]
    public async Task ログ行指定は他セッションを含めず除外後も境界時刻を維持する()
    {
        using var directory = new TemporaryDirectory();
        var session = LogExclusionTests.CreateSession(directory.Path);
        var vm = new AnalysisRangeViewModel();
        await vm.LoadRecordsAsync(session.Records.Concat(session.Records).ToArray(), CancellationToken.None);
        var excluded = new AnalysisExclusions { Records = [new("session", "3")] };
        session.SaveExclusions(excluded);
        vm.SetLogRange(session.Records, session.Records[0], session.Records[2], "装備Ａ", session.DisplayName,
            record => session.Exclusions.IsExcluded(record));
        var result = await Analyze(vm);
        Assert.Equal(1, result.ExcludedRecordCount);
        Assert.Equal(20, result.AnalysisTime.DurationSeconds);
        Assert.Equal("装備Ａ", vm.LastCompletedRangeName);
        session.SaveExclusions(new());
        result = await Analyze(vm);
        Assert.Equal(0, result.ExcludedRecordCount);
        Assert.Equal(10, Assert.Single(result.ActorSummaries, actor => actor.Actor == "Xitra").TotalDamage);
        Assert.Equal(3, vm.LastCompletedRecordCount);
        vm.IsManualRangeMode = true;
        Assert.False(vm.IsLogRangeMode);
        vm.IsLogRangeMode = true;
        await vm.LoadRecordsAsync(session.Records, CancellationToken.None);
        Assert.False(vm.IsLogRangeMode);
        vm.IsLogRangeMode = true;
        Assert.False(vm.RunAnalysisCommand.CanExecute(null));
    }

    [Fact]
    public async Task 分析区間画面から指定を反映しキャンセル時は区間を変更しない()
    {
        using var directory = new TemporaryDirectory();
        var settings = new LogRep2Settings();
        settings.Collection.OutputDirectory = directory.GetPath("sessions");
        new LogRep2SettingsStore(directory.Path).Save(settings);
        foreach (var name in new[] { "a", "b" })
        {
            var folder = directory.GetPath("sessions/" + name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "session.json"), """{"session_id":"session","status":"completed"}""");
            File.WriteAllText(Path.Combine(folder, "stats.json"), "{}");
            File.WriteAllLines(Path.Combine(folder, "canonical_records.jsonl"), LogExclusionTests.CreateRecords().Select(record => JsonSerializer.Serialize(record)));
        }
        var main = new MainViewModel(new SessionOpenService(), new DialogService(), new AnalyzerSettingsStore(directory.Path));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        var accept = true;
        main.LogRangeRequested += editor =>
        {
            SetRange(editor, 0, 2);
            editor.RangeName = "1セッションのみ";
            return accept;
        };
        Assert.True(main.AnalysisRange.OpenLogRangeCommand.CanExecute(null));
        main.AnalysisRange.OpenLogRangeCommand.Execute(null);
        Assert.True(main.AnalysisRange.IsLogRangeMode);
        var result = await Analyze(main.AnalysisRange);
        Assert.Equal(10, Assert.Single(result.ActorSummaries, actor => actor.Actor == "Xitra").TotalDamage);
        Assert.DoesNotContain(main.AnalysisResult.SessionRows, row => row.Label == "選択セッション数");
        var description = main.AnalysisRange.LogRangeDescription;
        accept = false;
        main.AnalysisRange.OpenLogRangeCommand.Execute(null);
        Assert.Equal(description, main.AnalysisRange.LogRangeDescription);
    }

    private static void SetRange(LogRangeViewModel vm, int start, int end)
    {
        vm.SelectedRow = vm.VisibleRows[start];
        vm.SetStartCommand.Execute(null);
        vm.SelectedRow = vm.VisibleRows[end];
        vm.SetEndCommand.Execute(null);
    }

    private static async Task<AnalysisResult> Analyze(AnalysisRangeViewModel vm)
    {
        var completion = new TaskCompletionSource<AnalysisResult>();
        void Completed(AnalysisResult result) => completion.TrySetResult(result);
        vm.AnalysisCompleted += Completed;
        try
        {
            Assert.True(vm.RunAnalysisCommand.CanExecute(null), vm.ValidationMessage);
            vm.RunAnalysisCommand.Execute(null);
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (vm.IsBusy) await Task.Delay(10, timeout.Token);
            return result;
        }
        finally { vm.AnalysisCompleted -= Completed; }
    }
}
