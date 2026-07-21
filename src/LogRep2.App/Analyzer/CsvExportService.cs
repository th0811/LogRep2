using System.Text;
using System.IO;

namespace FFXI_LogAnalyzer.App;

public sealed class CsvExportService
{
    private readonly DialogService _dialogService;

    public CsvExportService(DialogService? dialogService = null)
    {
        _dialogService = dialogService ?? new DialogService();
    }

    public bool Export(
        string defaultFileName,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var path = _dialogService.SelectCsvOutputPath(defaultFileName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        Write(path, headers, rows);
        return true;
    }

    internal static void Write(
        string path,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        using var writer = new StreamWriter(
            path,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.NewLine = "\r\n";
        writer.WriteLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", row.Select(Escape)));
        }
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
