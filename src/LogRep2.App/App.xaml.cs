using System.Windows;

namespace FfxiTempLogCollector.App;

public partial class App : System.Windows.Application
{
    public App()
    {
        // エントリポイントが Program.Main のため、生成された Main は使われない。
        // 共通スタイル (Theme/Styles.xaml) を読み込むためにここで明示的に呼び出す。
        InitializeComponent();
    }
}
