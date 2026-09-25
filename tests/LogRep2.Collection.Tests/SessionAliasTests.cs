using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class SessionAliasTests
{
    [Fact]
    public void 日本語と全角文字の保存復元と解除ができる()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionAnnotationsStore();
        Assert.Equal(string.Empty, store.Load(directory.Path, "session"));
        store.Save(directory.Path, "session", "  ソーティ／暗黒・装備Ａ 🗡️  ");
        var session = LogExclusionTests.CreateSession(directory.Path);
        session.LoadAlias();
        Assert.Equal("ソーティ／暗黒・装備Ａ 🗡️（session）", session.DisplayName);
        session.SaveAlias("  ");
        Assert.Equal("session", session.DisplayName);
        Assert.Equal(string.Empty, store.Load(directory.Path, "session"));
    }

    [Theory]
    [InlineData("改行\n禁止")]
    [InlineData("タブ\t禁止")]
    [InlineData("改行\u2028禁止")]
    public void 制御文字を保存しない(string alias)
    {
        using var directory = new TemporaryDirectory();
        Assert.Throws<ArgumentException>(() => new SessionAnnotationsStore().Save(directory.Path, "session", alias));
        Assert.False(File.Exists(directory.GetPath(SessionAnnotationsStore.FileName)));
    }

    [Fact]
    public void 文字数上限と保存失敗では既存のエイリアスを保つ()
    {
        using var directory = new TemporaryDirectory();
        var session = LogExclusionTests.CreateSession(directory.Path);
        session.SaveAlias(new string('あ', 100));
        Assert.Throws<ArgumentException>(() => session.SaveAlias(new string('あ', 101)));
        using (File.Open(directory.GetPath(SessionAnnotationsStore.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => session.SaveAlias("変更"));
        }
        Assert.Equal(new string('あ', 100), session.Alias);
        Assert.Equal(session.Alias, new SessionAnnotationsStore().Load(directory.Path, "session"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Theory]
    [InlineData("不正なJSON")]
    [InlineData("{\"schema_version\":2,\"session_id\":\"session\"}")]
    [InlineData("{\"session_id\":\"other\",\"alias\":\"別セッション\"}")]
    public void 不正な設定ではID表示に戻し保存で上書きしない(string json)
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.GetPath(SessionAnnotationsStore.FileName), json);
        var session = LogExclusionTests.CreateSession(directory.Path);
        session.LoadAlias();
        Assert.NotNull(session.AliasLoadError);
        Assert.Equal("session", session.DisplayName);
        Assert.Throws<InvalidOperationException>(() => session.SaveAlias("上書き"));
        Assert.Equal(json, File.ReadAllText(directory.GetPath(SessionAnnotationsStore.FileName)));
    }

    [Fact]
    public async Task 検索と編集で分析対象や区間を変えず再起動後も復元する()
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
            File.WriteAllText(Path.Combine(folder, "canonical_records.jsonl"), string.Empty);
        }
        var main = await Open(directory.Path);
        var first = main.Sessions.Single(session => session.SessionId == "first");
        first.SaveAlias("トアクリーバ検証");
        main.AnalysisRange.IsManualRangeMode = true;
        main.SessionSearchText = "検証";
        Assert.Same(first, Assert.Single(main.FilteredSessions));
        Assert.All(main.Sessions, session => Assert.True(session.IsEnabled));
        Assert.Contains("対象 2件", main.SessionSearchSummary);
        Assert.True(main.AnalysisRange.IsManualRangeMode);
        main.SelectedSession = first;
        first.SaveAlias("装備比較");
        Assert.Empty(main.FilteredSessions);
        Assert.Contains(main.SessionInfoRows, row => row.Value.Contains("装備比較"));
        main.SessionSearchText = "SECOND";
        Assert.Equal("second", Assert.Single(main.FilteredSessions).SessionId);
        main.SessionSearchText = string.Empty;
        Assert.Equal(2, main.FilteredSessions.Count);
        var reopened = await Open(directory.Path);
        Assert.Equal("装備比較", reopened.Sessions.Single(session => session.SessionId == "first").Alias);
        Assert.All(reopened.Sessions, session => Assert.True(session.IsEnabled));
        File.WriteAllText(Path.Combine(first.FolderPath, SessionAnnotationsStore.FileName), "不正");
        var broken = await Open(directory.Path);
        Assert.Contains(broken.Warnings, warning => warning.Contains("エイリアス"));
        Assert.True(broken.HasSession);
        Assert.Equal(2, broken.Sessions.Count);
    }

    private static async Task<MainViewModel> Open(string folder)
    {
        var main = new MainViewModel(new SessionOpenService(), new DialogService(), new AnalyzerSettingsStore(folder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (main.IsBusy) await Task.Delay(10, timeout.Token);
        return main;
    }
}
