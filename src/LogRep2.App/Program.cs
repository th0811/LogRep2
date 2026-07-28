using FfxiTempLogCollector.Core;
using FfxiTempLogCollector.Ipc;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        DiagnosticLogService.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                DiagnosticLogService.Write("未処理例外", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            DiagnosticLogService.Write("未監視タスク例外", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            return args.Length == 0
                ? RunGui()
                : RunCliAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            DiagnosticLogService.Write("致命的なエラー", exception);
            if (args.Length == 0)
            {
                System.Windows.MessageBox.Show(
                    "予期しないエラーが発生しました。診断ログを確認してください。"
                        + Environment.NewLine
                        + DiagnosticLogService.LogDirectory,
                    "LogRep2 エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            else
            {
                Console.Error.WriteLine($"予期しないエラーが発生しました: {exception.Message}");
            }

            return 1;
        }
    }

    private static int RunGui()
    {
        using var singleInstance = new SingleInstanceManager();

        if (!singleInstance.TryAcquire())
        {
            return ShowExistingInstanceAsync()
                .GetAwaiter()
                .GetResult();
        }

        if (OperatingSystem.IsWindows())
        {
            FreeConsole();
        }

        var application = new App();
        var unifiedSettingsStore = new LogRep2SettingsStore();
        var migration = LoadGuiSettingsWithRecovery(
            unifiedSettingsStore);
        if (migration is null)
        {
            return (int)CliExitCode.ConfigError;
        }
        var configStore = new ConfigStore();
        var configLoader = new ConfigLoader(configStore);
        var config = migration.Settings.CreateCollectorConfig(
            unifiedSettingsStore.ApplicationDirectory);
        var collectorService = new CollectorService();
        var configEditService = new ConfigEditService(
            configStore,
            collectorService,
            unifiedSettingsStore.SettingsPath,
            unifiedSettingsStore.SaveCollectorConfig);
        var controller = new GuiCommandController(
            collectorService,
            config,
            configEditService,
            configLoader,
            unifiedSettingsStore: unifiedSettingsStore);
        var realtimeAnalysis = new RealtimeAnalysisController(
            collectorService.Events,
            migration.Settings.Analysis.RealtimeRefreshIntervalMs);
        var overlayManager = new OverlayManager(
            unifiedSettingsStore,
            realtimeAnalysis,
            application.Dispatcher,
            message => controller.ShowOperationError(
                "オーバーレイでエラーが発生しました。",
                new InvalidOperationException(message)),
            () => controller.ShowPartyMemberSettings(
                realtimeAnalysis.Current.Result?.ActorSummaries
                    .Select(actor => actor.Actor)
                    ?? []));
        controller.AttachOverlayManager(overlayManager);
        controller.StartOverlayNotifications();
        var viewModel = new MainViewModel(
            controller,
            application.Dispatcher,
            realtimeAnalysis);
        using var updateHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        var updateService = new GitHubReleaseUpdateService(updateHttpClient);
        var mainWindow = new MainWindow(
            viewModel,
            updateService,
            config.CheckForUpdatesOnLaunch);
        controller.AttachWindow(mainWindow);
        application.DispatcherUnhandledException += (_, eventArgs) =>
        {
            DiagnosticLogService.Write("画面処理エラー", eventArgs.Exception);
            if (!IsOverlayException(eventArgs.Exception))
            {
                return;
            }

            eventArgs.Handled = true;
            controller.HideOverlay();
            System.Windows.MessageBox.Show(
                mainWindow,
                "オーバーレイでエラーが発生したため非表示にしました。"
                    + Environment.NewLine
                    + eventArgs.Exception.Message,
                "オーバーレイエラー",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        };

        if (migration.Warnings.Count > 0)
        {
            mainWindow.Loaded += (_, _) =>
                System.Windows.MessageBox.Show(
                    mainWindow,
                    string.Join(
                        Environment.NewLine + Environment.NewLine,
                        migration.Warnings),
                    "設定移行の警告",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
        }

        var server = new NamedPipeCommandServer(
            controller.HandleIpcCommandAsync);
        server.Start();

        try
        {
            return application.Run(mainWindow);
        }
        finally
        {
            server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        var output = CliOutputWriter.CreateConsole();
        var parseResult = new CliCommandParser().Parse(args);

        if (!parseResult.Success || parseResult.Command is null)
        {
            output.WriteError(parseResult.Error);
            output.WriteError(
                "使用方法を確認するには help を実行してください。");
            return (int)CliExitCode.InvalidArguments;
        }

        var configStore = new ConfigStore();
        var configLoader = new ConfigLoader(configStore);
        var unifiedSettingsStore = new LogRep2SettingsStore();
        var migration = unifiedSettingsStore.LoadOrMigrate();

        foreach (var warning in migration.Warnings)
        {
            output.WriteError(warning);
        }

        await using var controller = new CliCommandController(
            configStore,
            configLoader,
            new CollectorService(),
            output,
            new NamedPipeCommandClient(),
            unifiedSettingsStore);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler? cancelHandler = null;

        if (parseResult.Command.Kind == CliCommandKind.Start)
        {
            cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
        }

        try
        {
            var exitCode = await controller.ExecuteAsync(
                parseResult.Command,
                cancellation.Token);
            return (int)exitCode;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
    }

    private static SettingsMigrationResult? LoadGuiSettingsWithRecovery(
        LogRep2SettingsStore settingsStore)
    {
        try
        {
            return settingsStore.LoadOrMigrate();
        }
        catch (Exception exception)
        {
            if (!File.Exists(settingsStore.SettingsPath))
            {
                ShowSettingsStartupError(
                    "設定ファイルを作成できませんでした。"
                    + Environment.NewLine
                    + "書き込み可能なフォルダーへLogRep2を移動してください。",
                    settingsStore,
                    exception);
                return null;
            }

            var answer = System.Windows.MessageBox.Show(
                "設定ファイルを読み込めませんでした。"
                + Environment.NewLine
                + settingsStore.SettingsPath
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message
                + Environment.NewLine
                + Environment.NewLine
                + "破損した設定をバックアップし、初期設定で起動しますか？",
                "設定ファイルの復旧",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (answer != System.Windows.MessageBoxResult.Yes)
            {
                return null;
            }

            try
            {
                var backupPath = settingsStore.BackupInvalidSettings();
                var recovered = settingsStore.LoadOrMigrate();
                System.Windows.MessageBox.Show(
                    "設定を初期化しました。破損した設定は次の場所へバックアップしました。"
                    + Environment.NewLine
                    + backupPath,
                    "設定ファイルの復旧",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return recovered;
            }
            catch (Exception recoveryException)
            {
                ShowSettingsStartupError(
                    "設定ファイルを復旧できませんでした。",
                    settingsStore,
                    recoveryException);
                return null;
            }
        }
    }

    private static void ShowSettingsStartupError(
        string message,
        LogRep2SettingsStore settingsStore,
        Exception exception)
    {
        System.Windows.MessageBox.Show(
            message
            + Environment.NewLine
            + settingsStore.SettingsPath
            + Environment.NewLine
            + Environment.NewLine
            + exception.Message,
            "設定エラー",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private static async Task<int> ShowExistingInstanceAsync()
    {
        var client = new NamedPipeCommandClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.TrySendAsync(
                new IpcCommand { Name = "show" },
                TimeSpan.FromSeconds(2));

            if (response is not null)
            {
                return response.Success ? 0 : 1;
            }

            await Task.Delay(200);
        }

        return 1;
    }

    private static bool IsOverlayException(Exception exception)
    {
        var detail = exception.ToString();
        return detail.Contains("OverlayWindow", StringComparison.Ordinal)
            || detail.Contains("OverlayViewModel", StringComparison.Ordinal)
            || detail.Contains("OverlayManager", StringComparison.Ordinal);
    }
}
