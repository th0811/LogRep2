using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace FfxiTempLogCollector.App;

internal static class DiagnosticLogService
{
    private static readonly object Sync = new();
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    public static string LogDirectory => _logDirectory;

    public static string VersionText
    {
        get
        {
            var assembly = typeof(DiagnosticLogService).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var version = informationalVersion?.Split('+')[0]
                ?? assembly.GetName().Version?.ToString(3)
                ?? "不明";
            return $"バージョン {version}";
        }
    }

    public static void Initialize(string? applicationDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(applicationDirectory))
        {
            _logDirectory = Path.Combine(applicationDirectory, "logs");
        }

        TryDeleteOldLogs();
        Write("起動", "LogRep2を起動しました。");
    }

    public static void Write(string category, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(category, exception.ToString());
    }

    public static void Write(string category, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(
                    _logDirectory,
                    $"LogRep2-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(
                    path,
                    BuildEntry(DateTimeOffset.Now, category, message),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
        }
        catch
        {
            // 診断ログの失敗によって本来の処理を停止させない。
        }
    }

    public static void OpenLogDirectory()
    {
        Directory.CreateDirectory(_logDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _logDirectory,
            UseShellExecute = true,
        });
    }

    internal static string BuildEntry(
        DateTimeOffset timestamp,
        string category,
        string message)
    {
        return $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{category}]"
            + Environment.NewLine
            + message
            + Environment.NewLine
            + Environment.NewLine;
    }

    private static void TryDeleteOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var threshold = DateTime.Now.AddDays(-30);
            foreach (var path in Directory.EnumerateFiles(
                         _logDirectory,
                         "LogRep2-*.log"))
            {
                if (File.GetLastWriteTime(path) < threshold)
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // 古いログを削除できなくても起動は継続する。
        }
    }
}
