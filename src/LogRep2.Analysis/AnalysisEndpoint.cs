namespace FFXI_LogAnalyzer.Core;

public sealed record AnalysisEndpoint(AnalysisEndpointType Type, MarkerRecord? Marker)
{
    public LogRep2.Contracts.ICanonicalRecord? Record { get; init; }

    public static AnalysisEndpoint FromRecord(LogRep2.Contracts.ICanonicalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(AnalysisEndpointType.Record, null) { Record = record };
    }

    public static AnalysisEndpoint LogStart { get; } = new(AnalysisEndpointType.LogStart, null);

    public static AnalysisEndpoint LogEnd { get; } = new(AnalysisEndpointType.LogEnd, null);

    public static AnalysisEndpoint FromMarker(MarkerRecord marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return new AnalysisEndpoint(AnalysisEndpointType.Marker, marker);
    }
}
