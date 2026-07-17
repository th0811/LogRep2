using System.Text.Json;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class JsonlWriterTests
{
    [Fact]
    public void RawRecordを一行Jsonとして追記できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var writer = new RawRecordJsonlWriter();

        writer.Append(
            temporaryDirectory.Path,
            RawRecordTestData.Create(rawRecordId: "raw-1"));
        writer.Append(
            temporaryDirectory.Path,
            RawRecordTestData.Create(rawRecordId: "raw-2"));

        var path = temporaryDirectory.GetPath(
            RawRecordJsonlWriter.FileName);
        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);

        using var first = JsonDocument.Parse(lines[0]);
        Assert.Equal(
            SchemaVersions.RawRecord,
            first.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal(
            "raw-1",
            first.RootElement.GetProperty("raw_record_id").GetString());
        Assert.Equal(
            "success",
            first.RootElement.GetProperty("parse_status").GetString());
    }

    [Fact]
    public void CanonicalRecordをOrder順で全体再書き込みできる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var factory = new CanonicalRecordFactory();
        var writer = new CanonicalRecordJsonlWriter();
        var first = factory.Create(
            RawRecordTestData.Create(
                rawRecordId: "raw-1",
                visibleText: "first"),
            1);
        var second = factory.Create(
            RawRecordTestData.Create(
                rawRecordId: "raw-2",
                visibleText: "second"),
            2);

        writer.WriteAll(temporaryDirectory.Path, [second, first]);

        var path = temporaryDirectory.GetPath(
            CanonicalRecordJsonlWriter.FileName);
        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);

        using var firstLine = JsonDocument.Parse(lines[0]);
        using var secondLine = JsonDocument.Parse(lines[1]);
        Assert.Equal(
            SchemaVersions.CanonicalRecord,
            firstLine.RootElement
                .GetProperty("schema_version")
                .GetString());
        Assert.Equal(
            1,
            firstLine.RootElement.GetProperty("order").GetInt64());
        Assert.Equal(
            2,
            secondLine.RootElement.GetProperty("order").GetInt64());

        writer.WriteAll(temporaryDirectory.Path, [second]);
        Assert.Single(File.ReadAllLines(path));
    }
}
