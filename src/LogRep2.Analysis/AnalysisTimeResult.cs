namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisTimeResult
{
    public AnalysisTimeResult(
        TimeConfidence confidence,
        double? durationSeconds,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        IReadOnlyList<string> warnings)
    {
        Confidence = confidence;
        DurationSeconds = durationSeconds;
        StartTime = startTime;
        EndTime = endTime;
        Warnings = warnings;
    }

    public TimeConfidence Confidence { get; }

    public double? DurationSeconds { get; }

    public DateTimeOffset? StartTime { get; }

    public DateTimeOffset? EndTime { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool CanCalculateDps => DurationSeconds is > 0;

    public static AnalysisTimeResult Unknown(IReadOnlyList<string> warnings)
    {
        return new AnalysisTimeResult(TimeConfidence.Unknown, null, null, null, warnings);
    }
}
