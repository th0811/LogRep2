using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;
using System.Text.Json;

namespace FfxiTempLogCollector.Tests;

public sealed class AnalysisExclusionIntegrationTests
{
    [Fact]
    public async Task マーカーの区間を保持し経験値の行除外も反映する()
    {
        var records = new CanonicalRecord[]
        {
            new() { Order = 1, IsMarker = true, MarkerKeyword = "開始", MessageTimeText = "[10:00:00]" },
            new() { Order = 2, VisibleText = "Xitraは、1024経験値を獲得した。" },
            new() { Order = 3, VisibleText = "Xitraは、24経験値を獲得した。" },
            new() { Order = 4, IsMarker = true, MarkerKeyword = "終了", MessageTimeText = "[10:00:30]" },
        };
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(records, CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        viewModel.SelectedStartMarker = viewModel.Markers[0];
        viewModel.SelectedEndMarker = viewModel.Markers[1];
        var start = viewModel.SelectedStartMarker;
        var end = viewModel.SelectedEndMarker;
        viewModel.SetExcludedRecords([records[0], records[1], records[3]]);
        var result = await Analyze(viewModel);
        Assert.Same(start, viewModel.SelectedStartMarker);
        Assert.Same(end, viewModel.SelectedEndMarker);
        Assert.Equal(30, result.AnalysisTime.DurationSeconds);
        Assert.Equal(24, result.LevelingPointSummaries.Single(summary => summary.PointName == "経験値").TotalPoints);
        Assert.Equal(1, result.ExcludedRecordCount);
    }

    [Fact]
    public async Task 不正な設定のセッションを外せば残りを分析できる()
    {
        using var directory = new TemporaryDirectory();
        var first = WriteSession(directory, "first");
        WriteSession(directory, "second");
        File.WriteAllText(Path.Combine(first, AnalysisExclusionsStore.FileName), "不正なJSON");
        var main = await OpenMain(directory.Path);
        main.AnalysisRange.IsManualRangeMode = true;
        Assert.False(main.AnalysisRange.RunAnalysisCommand.CanExecute(null));
        Assert.Contains(main.Warnings, warning => warning.Contains("除外設定"));
        var bad = main.Sessions.Single(session => session.FolderPath == Path.GetFullPath(first));
        bad.IsEnabled = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!main.AnalysisRange.IsAreaSegmentMode) await Task.Delay(10, timeout.Token);
        main.AnalysisRange.IsManualRangeMode = true;
        var result = await Analyze(main.AnalysisRange);
        Assert.Equal(110, Assert.Single(result.ActorSummaries).TotalDamage);
    }

    [Fact]
    public async Task 除外画面の保存後に古い結果を消し再起動後も元ログを変えず復元する()
    {
        using var directory = new TemporaryDirectory();
        var folder = WriteSession(directory, "first");
        var logPath = Path.Combine(folder, "canonical_records.jsonl");
        var source = File.ReadAllText(logPath);
        var main = await OpenMain(directory.Path);
        main.AnalysisRange.IsManualRangeMode = true;
        await Analyze(main.AnalysisRange);
        Assert.True(main.AnalysisResult.HasResult);
        main.LogExclusionsRequested += editor => editor.Apply(LogExclusionOperation.ExcludeGroups, [editor.VisibleRows[0]]);
        main.OpenLogExclusionsCommand.Execute(null);
        Assert.False(main.AnalysisResult.HasResult);
        Assert.True(main.AnalysisRange.IsManualRangeMode);
        Assert.Equal(1, main.SelectedTabIndex);
        var result = await Analyze(main.AnalysisRange);
        Assert.Equal(100, Assert.Single(result.ActorSummaries).TotalDamage);
        Assert.Contains("3行", main.AnalysisResult.ExclusionSummary);

        var reopened = await OpenMain(directory.Path);
        reopened.AnalysisRange.IsManualRangeMode = true;
        var restored = await Analyze(reopened.AnalysisRange);
        Assert.Equal(100, Assert.Single(restored.ActorSummaries).TotalDamage);
        Assert.Equal(source, File.ReadAllText(logPath));
    }

    [Fact]
    public async Task 同じIDのセッションを別フォルダから結合しても除外は保存先だけに適用する()
    {
        using var directory = new TemporaryDirectory();
        var first = WriteSession(directory, "first");
        WriteSession(directory, "second");
        new AnalysisExclusionsStore().Save(first, new AnalysisExclusions
        {
            Groups = [new("session", "g1"), new("session", "g2")],
        });
        var main = await OpenMain(directory.Path);
        main.AnalysisRange.IsManualRangeMode = true;
        var result = await Analyze(main.AnalysisRange);
        Assert.Equal(110, Assert.Single(result.ActorSummaries).TotalDamage);
        Assert.Equal(5, result.ExcludedRecordCount);
    }

    private static string WriteSession(TemporaryDirectory directory, string name)
    {
        var settings = new LogRep2Settings();
        settings.Collection.OutputDirectory = directory.GetPath("sessions");
        new LogRep2SettingsStore(directory.Path).Save(settings);
        var folder = directory.GetPath("sessions/" + name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "session.json"), """{"session_id":"session","status":"completed"}""");
        File.WriteAllText(Path.Combine(folder, "stats.json"), "{}");
        File.WriteAllLines(Path.Combine(folder, "canonical_records.jsonl"),
            LogExclusionTests.CreateRecords().Select(record => JsonSerializer.Serialize(record)));
        return folder;
    }

    private static async Task<MainViewModel> OpenMain(string folder)
    {
        var main = new MainViewModel(new SessionOpenService(), new DialogService(), new AnalyzerSettingsStore(folder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        Assert.DoesNotContain("失敗", main.StatusMessage);
        return main;
    }

    [Fact]
    public async Task グループ除外で使用回数とダメージを除き時間と区間選択を維持する()
    {
        var records = LogExclusionTests.CreateRecords();
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(records, CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        var baseline = await Analyze(viewModel);
        Assert.Equal(110, Assert.Single(baseline.ActorSummaries).TotalDamage);
        Assert.Equal(2, Assert.Single(baseline.ActionSummaries).UseCount);

        viewModel.SetExcludedRecords(records.Take(3));
        Assert.True(viewModel.IsManualRangeMode);
        var excluded = await Analyze(viewModel);
        var actor = Assert.Single(excluded.ActorSummaries);
        Assert.Equal(100, actor.TotalDamage);
        Assert.Equal(1, actor.TotalUseCount);
        Assert.Equal(5, actor.Dps);
        Assert.Equal(baseline.AnalysisTime.DurationSeconds, excluded.AnalysisTime.DurationSeconds);
        Assert.Equal(3, excluded.ExcludedRecordCount);
        Assert.Equal(1, excluded.ExcludedGroupCount);
        Assert.Equal(2, viewModel.LastCompletedRecordCount);

        viewModel.SetExcludedRecords([]);
        var restored = await Analyze(viewModel);
        Assert.Equal(110, Assert.Single(restored.ActorSummaries).TotalDamage);
        Assert.Equal(0, restored.ExcludedRecordCount);
    }

    [Fact]
    public async Task ダメージ行だけの除外では残りの行を再解析する()
    {
        var records = LogExclusionTests.CreateRecords();
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(records, CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        viewModel.SetExcludedRecords([records[2]]);
        var result = await Analyze(viewModel);
        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal(100, actor.TotalDamage);
        Assert.Equal(2, actor.TotalUseCount);
        Assert.Equal(1, actor.UnknownCount);
        Assert.Single(result.UnknownActionGroups);
    }

    [Fact]
    public async Task 全行除外でも分析時間を維持し空の結果を返す()
    {
        var records = LogExclusionTests.CreateRecords();
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(records, CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        viewModel.SetExcludedRecords(records);
        var result = await Analyze(viewModel);
        Assert.Empty(result.ActorSummaries);
        Assert.Empty(result.UnparsedActionGroups);
        Assert.Equal(20, result.AnalysisTime.DurationSeconds);
        Assert.Equal(5, result.ExcludedRecordCount);
        Assert.Equal(0, viewModel.LastCompletedRecordCount);
    }

    [Fact]
    public async Task 除外設定の読込エラーがあると分析を開始できない()
    {
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(LogExclusionTests.CreateRecords(), CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        viewModel.SetExcludedRecords([], "除外設定の読込エラー");
        Assert.False(viewModel.RunAnalysisCommand.CanExecute(null));
        Assert.Equal("除外設定の読込エラー", viewModel.ValidationMessage);
        viewModel.SetExcludedRecords([]);
        Assert.True(viewModel.RunAnalysisCommand.CanExecute(null));
    }

    internal static async Task<AnalysisResult> Analyze(AnalysisRangeViewModel viewModel)
    {
        var completion = new TaskCompletionSource<AnalysisResult>();
        void Completed(AnalysisResult result) => completion.TrySetResult(result);
        viewModel.AnalysisCompleted += Completed;
        try
        {
            Assert.True(viewModel.RunAnalysisCommand.CanExecute(null));
            viewModel.RunAnalysisCommand.Execute(null);
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (viewModel.IsBusy) await Task.Delay(10, timeout.Token);
            return result;
        }
        finally
        {
            viewModel.AnalysisCompleted -= Completed;
        }
    }
}
