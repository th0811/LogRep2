using System.Windows;

namespace FFXI_LogAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new SessionOpenService(),
            new DialogService());
    }
}
