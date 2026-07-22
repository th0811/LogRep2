namespace FFXI_LogAnalyzer.App;

public partial class AnalysisResultView : System.Windows.Controls.UserControl
{
    public AnalysisResultView()
    {
        InitializeComponent();
    }

    private void OnRegistrationButtonClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }
}
