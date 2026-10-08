using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class SessionExclusionBadgeTests
{
    [Fact]
    public void 除外設定の保存と読込でバッジ件数とエラー表示が更新される()
    {
        using var directory = new TemporaryDirectory();
        var session = LogExclusionTests.CreateSession(directory.Path);
        var notifications = new List<string?>();
        session.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        Assert.False(session.HasExclusionBadge);
        var exclusions = new AnalysisExclusions();
        exclusions.Records.Add(new("session", "現在のログにない行"));
        exclusions.Groups.Add(AnalysisExclusions.TryGetGroupKey(session.Records[0])!);
        session.SaveExclusions(exclusions);
        Assert.Equal("除外あり", session.ExclusionBadgeText);
        Assert.Contains("行単位：1件／グループ単位：1件", session.ExclusionBadgeToolTip);
        Assert.Contains(nameof(session.HasExclusionBadge), notifications);
        Assert.Contains(nameof(session.ExclusionBadgeToolTip), notifications);

        var reopened = LogExclusionTests.CreateSession(directory.Path);
        reopened.LoadExclusions();
        Assert.True(reopened.HasExclusionBadge);
        Assert.Equal(session.ExclusionBadgeToolTip, reopened.ExclusionBadgeToolTip);
        using (File.Open(Path.Combine(directory.Path, AnalysisExclusionsStore.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<Exception>(() => reopened.SaveExclusions(new()));
        Assert.True(reopened.HasExclusionSettings);
        reopened.SaveExclusions(new());
        Assert.False(reopened.HasExclusionBadge);

        File.WriteAllText(Path.Combine(directory.Path, AnalysisExclusionsStore.FileName), "不正なJSON");
        reopened.LoadExclusions();
        Assert.True(reopened.HasExclusionsLoadError);
        Assert.Equal("除外設定エラー", reopened.ExclusionBadgeText);
        Assert.Contains(reopened.ExclusionsLoadError!, reopened.ExclusionBadgeToolTip);
        new AnalysisExclusionsStore().Save(directory.Path, new());
        reopened.LoadExclusions();
        Assert.False(reopened.HasExclusionBadge);
        Assert.False(reopened.HasExclusionsLoadError);
    }

    [Fact]
    public async Task 注意文は非表示の分析対象も数えバッジから該当セッションを開ける()
    {
        using var directory = new TemporaryDirectory();
        var settings = new LogRep2Settings();
        settings.Collection.OutputDirectory = directory.GetPath("sessions");
        new LogRep2SettingsStore(directory.Path).Save(settings);
        foreach (var id in new[] { "first", "second" })
        {
            var folder = directory.GetPath("sessions/" + id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "session.json"), $$"""{"session_id":"{{id}}","status":"completed"}""");
            File.WriteAllText(Path.Combine(folder, "stats.json"), "{}");
            File.WriteAllText(Path.Combine(folder, "canonical_records.jsonl"), "");
        }
        var main = new MainViewModel(new SessionOpenService(), new DialogService(), new AnalyzerSettingsStore(directory.Path));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        var first = main.Sessions.Single(session => session.SessionId == "first");
        var second = main.Sessions.Single(session => session.SessionId == "second");
        var notifications = new List<string?>();
        main.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        Assert.False(main.HasSessionExclusionSummary);
        var exclusions = new AnalysisExclusions();
        exclusions.Records.Add(new("first", "1"));
        first.SaveExclusions(exclusions);
        Assert.Contains("1セッションに除外設定", main.SessionExclusionSummary);
        Assert.Contains(nameof(main.SessionExclusionSummary), notifications);
        main.SessionSearchText = "second";
        Assert.Single(main.FilteredSessions);
        Assert.True(main.HasSessionExclusionSummary);
        first.IsEnabled = false;
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        Assert.False(main.HasSessionExclusionSummary);
        first.IsEnabled = true;
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        Assert.True(main.HasSessionExclusionSummary);

        main.SelectedSession = second;
        LogExclusionViewModel? editor = null;
        main.LogExclusionsRequested += value => editor = value;
        main.OpenSessionLogExclusions(first);
        Assert.NotNull(editor);
        Assert.Same(first, editor.SelectedSession);
        first.SaveExclusions(new());
        Assert.False(main.HasSessionExclusionSummary);
        File.WriteAllText(Path.Combine(first.FolderPath, AnalysisExclusionsStore.FileName), "不正");
        first.LoadExclusions();
        Assert.Contains("1セッションの除外設定を読み込めません", main.SessionExclusionSummary);
    }
}
