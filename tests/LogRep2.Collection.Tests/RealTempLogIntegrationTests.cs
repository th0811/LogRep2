using System.Text.Json;
using System.Text.RegularExpressions;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed partial class RealTempLogIntegrationTests
{
    [Fact]
    public void 実ログサンプルをOnce収集してCompletedセッションを生成する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var fixtureDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "temp_logs");
        var outputDirectory =
            temporaryDirectory.GetPath("sessions");
        Assert.True(Directory.Exists(fixtureDirectory));

        var result = new OnceCollectionRunner().Run(
            new CollectorConfig
            {
                TempDir = fixtureDirectory,
                OutputDir = outputDirectory,
                WatchWindow1 = true,
                WatchWindow2 = true,
                RotationSlots = 2,
            });

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.FilesRead);
        Assert.True(result.RawRecordsWritten > 0);
        Assert.True(result.CanonicalRecordsWritten > 0);
        Assert.True(
            result.CanonicalRecordsWritten
            <= result.RawRecordsWritten);

        var rawPath = Path.Combine(
            result.SessionDirectory,
            RawRecordJsonlWriter.FileName);
        var canonicalPath = Path.Combine(
            result.SessionDirectory,
            CanonicalRecordJsonlWriter.FileName);
        var sessionPath = Path.Combine(
            result.SessionDirectory,
            SessionManager.SessionFileName);

        Assert.True(File.Exists(rawPath));
        Assert.True(File.Exists(canonicalPath));
        Assert.True(File.Exists(sessionPath));

        var rawRecords = ReadJsonLines(rawPath);
        var canonicalRecords = ReadJsonLines(canonicalPath);
        Assert.NotEmpty(rawRecords);
        Assert.NotEmpty(canonicalRecords);
        Assert.Contains(
            rawRecords,
            record => !string.IsNullOrWhiteSpace(
                record.GetProperty("visible_text").GetString()));
        Assert.Contains(
            rawRecords,
            record => JapaneseTextRegex().IsMatch(
                record.GetProperty("visible_text").GetString()
                ?? string.Empty));

        VerifyOptionalTimestamps(rawRecords);
        VerifyOptionalMarkers(rawRecords);

        using var sessionDocument = JsonDocument.Parse(
            File.ReadAllText(sessionPath));
        var session = sessionDocument.RootElement;
        Assert.False(
            string.IsNullOrWhiteSpace(
                session.GetProperty("schema_version").GetString()));
        Assert.Equal(
            "completed",
            session.GetProperty("status").GetString());
    }

    private static List<JsonElement> ReadJsonLines(string path)
    {
        return
        [
            .. File.ReadLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(
                    line => JsonDocument.Parse(line)
                        .RootElement.Clone()),
        ];
    }

    private static void VerifyOptionalTimestamps(
        IReadOnlyList<JsonElement> records)
    {
        var recordsWithTimestampText = records
            .Where(
                record => TimestampRegex().IsMatch(
                    record.GetProperty("visible_text").GetString()
                    ?? string.Empty))
            .ToArray();

        foreach (var record in recordsWithTimestampText)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(
                    record.GetProperty("message_time_text")
                        .GetString()));
        }
    }

    private static void VerifyOptionalMarkers(
        IReadOnlyList<JsonElement> records)
    {
        var markerCandidates = records
            .Where(
                record => MarkerRegex().IsMatch(
                    record.GetProperty("visible_text").GetString()
                    ?? string.Empty))
            .ToArray();

        foreach (var record in markerCandidates)
        {
            Assert.True(record.GetProperty("is_marker").GetBoolean());
            Assert.False(
                string.IsNullOrWhiteSpace(
                    record.GetProperty("marker_keyword").GetString()));
        }
    }

    [GeneratedRegex("[ぁ-んァ-ヶ一-龠]")]
    private static partial Regex JapaneseTextRegex();

    [GeneratedRegex(@"\[\d{2}:\d{2}(?::\d{2})?\]")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"###\S+")]
    private static partial Regex MarkerRegex();
}
