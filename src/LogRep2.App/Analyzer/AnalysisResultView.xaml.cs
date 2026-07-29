namespace FFXI_LogAnalyzer.App;

public partial class AnalysisResultView : System.Windows.Controls.UserControl
{
    public AnalysisResultView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 分割ボタン（CSV出力・登録名など）。クリックで自前の ContextMenu をその場に開く。
    /// </summary>
    private void OnSplitButtonClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement =
            System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }
}
