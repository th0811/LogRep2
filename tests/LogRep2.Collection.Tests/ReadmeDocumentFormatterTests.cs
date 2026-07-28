using FfxiTempLogCollector.App;

namespace LogRep2.Collection.Tests;

public sealed class ReadmeDocumentFormatterTests
{
    [Fact]
    public void ToHtml_見出し表コードをHTMLへ変換する()
    {
        const string markdown = """
            # LogRep2

            | 項目 | 内容 |
            | --- | --- |
            | 表示 | README |

            `LogRep2.exe`
            """;

        var html = ReadmeDocumentFormatter.ToHtml(
            markdown,
            AppContext.BaseDirectory);

        Assert.Contains("<h1", html, StringComparison.Ordinal);
        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<code>LogRep2.exe</code>", html, StringComparison.Ordinal);
        Assert.Contains("README", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToHtml_埋め込みHTMLを無効化する()
    {
        const string markdown = "<script>alert('test')</script>";

        var html = ReadmeDocumentFormatter.ToHtml(
            markdown,
            AppContext.BaseDirectory);

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }
}
