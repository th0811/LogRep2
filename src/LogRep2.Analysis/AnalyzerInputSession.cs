namespace FFXI_LogAnalyzer.Core;

public sealed class AnalyzerInputSession
{
    public AnalyzerInputSession(
        string folderPath,
        string sessionJsonPath,
        string canonicalRecordsPath,
        string statsJsonPath,
        string? rawRecordsPath,
        SessionInfo sessionInfo,
        StatsInfo statsInfo)
    {
        FolderPath = folderPath;
        SessionJsonPath = sessionJsonPath;
        CanonicalRecordsPath = canonicalRecordsPath;
        StatsJsonPath = statsJsonPath;
        RawRecordsPath = rawRecordsPath;
        SessionInfo = sessionInfo;
        StatsInfo = statsInfo;
    }

    public string FolderPath { get; }

    public string SessionJsonPath { get; }

    public string CanonicalRecordsPath { get; }

    public string StatsJsonPath { get; }

    public string? RawRecordsPath { get; }

    public bool HasRawRecords => RawRecordsPath is not null;

    public SessionInfo SessionInfo { get; }

    public StatsInfo StatsInfo { get; }
}
