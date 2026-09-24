using FFXI_LogAnalyzer.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class SessionSelectionPersistenceTests
{
    [Fact]
    public async Task チェック変更を次回起動に復元し新規セッションは選択する()
    {
        using var directory = new TemporaryDirectory();
        Configure(directory);
        CreateSession(directory, "first");
        var first = await OpenAsync(directory);
        Assert.True(Assert.Single(first.Sessions).IsEnabled);

        first.Sessions[0].IsEnabled = false;
        Assert.False(first.HasSession);
        CreateSession(directory, "second");

        var reopened = await OpenAsync(directory);
        Assert.False(reopened.Sessions.Single(session => session.SessionId == "first").IsEnabled);
        Assert.True(reopened.Sessions.Single(session => session.SessionId == "second").IsEnabled);

        reopened.DisableAllSessionsCommand.Execute(null);
        await WaitAsync(() => reopened.DisableAllSessionsCommand.CanExecute(null));
        var disabled = await OpenAsync(directory);
        Assert.All(disabled.Sessions, session => Assert.False(session.IsEnabled));
        Assert.False(disabled.HasSession);

        disabled.EnableAllSessionsCommand.Execute(null);
        await WaitAsync(() => disabled.EnableAllSessionsCommand.CanExecute(null));
        var enabled = await OpenAsync(directory);
        Assert.All(enabled.Sessions, session => Assert.True(session.IsEnabled));
    }

    [Fact]
    public async Task 読込失敗したセッションの選択状態を上書きしない()
    {
        using var directory = new TemporaryDirectory();
        Configure(directory);
        var folder = CreateSession(directory, "first");
        var store = new AnalyzerSettingsStore(directory.Path);
        store.SaveSessionSelections([
            new() { FolderPath = folder, SessionId = "first", IsEnabled = false },
        ]);
        File.WriteAllText(Path.Combine(folder, "session.json"), "不正なJSON");
        CreateSession(directory, "second");
        var viewModel = await OpenAsync(directory);
        Assert.Single(viewModel.Sessions);
        viewModel.EnableAllSessionsCommand.Execute(null);
        await WaitAsync(() => viewModel.EnableAllSessionsCommand.CanExecute(null));

        CreateSession(directory, "first");
        var reopened = await OpenAsync(directory);
        Assert.False(reopened.Sessions.Single(session => session.SessionId == "first").IsEnabled);
    }

    private static void Configure(TemporaryDirectory directory)
    {
        var settings = new LogRep2Settings();
        settings.Collection.OutputDirectory = directory.GetPath("sessions");
        new LogRep2SettingsStore(directory.Path).Save(settings);
    }

    private static string CreateSession(TemporaryDirectory directory, string id)
    {
        var folder = directory.GetPath("sessions/" + id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "session.json"),
            $$"""{"session_id":"{{id}}","status":"completed"}""");
        File.WriteAllText(Path.Combine(folder, "stats.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "canonical_records.jsonl"), string.Empty);
        return folder;
    }

    private static async Task<MainViewModel> OpenAsync(TemporaryDirectory directory)
    {
        var viewModel = new MainViewModel(
            new SessionOpenService(), new DialogService(),
            new AnalyzerSettingsStore(directory.Path));
        await WaitAsync(() => !viewModel.IsBusy);
        Assert.DoesNotContain("失敗", viewModel.StatusMessage);
        return viewModel;
    }

    private static async Task WaitAsync(Func<bool> completed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!completed())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
