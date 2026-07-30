namespace FfxiTempLogCollector.Tests;

/// <summary>
/// 画面のスタイルは App.xaml でマージした Theme/Styles.xaml に入っているため、
/// ウィンドウ単体を生成するテストでも Application を1つ用意しておく必要がある。
/// </summary>
internal static class WpfTestApplication
{
    private static readonly object Gate = new();

    /// <summary>
    /// STAスレッド上から呼び出すこと。AppDomain 全体で Application は1つだけ生成する。
    /// </summary>
    internal static void Ensure()
    {
        lock (Gate)
        {
            if (System.Windows.Application.Current is not null)
            {
                return;
            }

            _ = new FfxiTempLogCollector.App.App();
        }
    }
}
