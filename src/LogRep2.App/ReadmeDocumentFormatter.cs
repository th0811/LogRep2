using System.IO;
using System.Net;
using Markdig;

namespace FfxiTempLogCollector.App;

internal static class ReadmeDocumentFormatter
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

    public static string ToHtml(
        string markdown,
        string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var markdownHtml = Markdown.ToHtml(
            markdown,
            Pipeline);
        var baseUri = CreateBaseUri(baseDirectory);

        return $$"""
            <!DOCTYPE html>
            <html lang="ja">
            <head>
              <meta charset="utf-8">
              <meta http-equiv="X-UA-Compatible" content="IE=edge">
              <base href="{{WebUtility.HtmlEncode(baseUri)}}">
              <style>
                body {
                  margin: 0 auto;
                  padding: 28px 36px 48px;
                  max-width: 980px;
                  color: #24292f;
                  background: #ffffff;
                  font-family: "Yu Gothic UI", "Meiryo UI", Meiryo, sans-serif;
                  font-size: 15px;
                  line-height: 1.65;
                }
                h1, h2, h3, h4, h5, h6 {
                  margin-top: 1.4em;
                  margin-bottom: 0.6em;
                  line-height: 1.25;
                  color: #1f2937;
                }
                h1, h2 {
                  padding-bottom: 0.3em;
                  border-bottom: 1px solid #d8dee4;
                }
                h1 { margin-top: 0; font-size: 2em; }
                h2 { font-size: 1.5em; }
                h3 { font-size: 1.25em; }
                a { color: #0969da; text-decoration: none; }
                a:hover { text-decoration: underline; }
                code {
                  padding: 0.15em 0.35em;
                  background: #eff1f3;
                  border-radius: 4px;
                  font-family: Consolas, "Courier New", monospace;
                  font-size: 0.9em;
                }
                pre {
                  overflow-x: auto;
                  padding: 14px 16px;
                  background: #f6f8fa;
                  border: 1px solid #d8dee4;
                  border-radius: 6px;
                  line-height: 1.45;
                }
                pre code {
                  padding: 0;
                  background: transparent;
                  border-radius: 0;
                }
                blockquote {
                  margin-left: 0;
                  padding: 0 1em;
                  color: #57606a;
                  border-left: 4px solid #d0d7de;
                }
                table {
                  display: block;
                  overflow-x: auto;
                  max-width: 100%;
                  border-spacing: 0;
                  border-collapse: collapse;
                }
                th, td {
                  padding: 7px 12px;
                  border: 1px solid #d0d7de;
                  text-align: left;
                  vertical-align: top;
                }
                th { background: #f6f8fa; }
                tr:nth-child(even) { background: #fbfcfd; }
                img { max-width: 100%; }
                hr {
                  height: 1px;
                  margin: 24px 0;
                  background: #d8dee4;
                  border: 0;
                }
              </style>
            </head>
            <body>
            {{markdownHtml}}
            </body>
            </html>
            """;
    }

    private static string CreateBaseUri(string baseDirectory)
    {
        var fullPath = Path.GetFullPath(baseDirectory);
        if (!Path.EndsInDirectorySeparator(fullPath))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        return new Uri(fullPath).AbsoluteUri;
    }
}
