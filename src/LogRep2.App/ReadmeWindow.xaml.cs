using System.IO;
using System.Windows;

namespace FfxiTempLogCollector.App;

public partial class ReadmeWindow : Window
{
    internal ReadmeWindow(string readmePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readmePath);

        InitializeComponent();

        var markdown = File.ReadAllText(readmePath);
        var baseDirectory = Path.GetDirectoryName(readmePath)
            ?? AppContext.BaseDirectory;
        ReadmeBrowser.NavigateToString(
            ReadmeDocumentFormatter.ToHtml(
                markdown,
                baseDirectory));
    }
}
