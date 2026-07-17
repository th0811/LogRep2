namespace FFXI_LogAnalyzer.Core;

public sealed record SchemaVersionInfo(
    string? SchemaVersion,
    string? RawSchemaVersion,
    string? CanonicalSchemaVersion);
