namespace FFXI_LogAnalyzer.Core;

public sealed record AnalysisEndpoint(AnalysisEndpointType Type, MarkerRecord? Marker)
{
    public static AnalysisEndpoint LogStart { get; } = new(AnalysisEndpointType.LogStart, null);

    public static AnalysisEndpoint LogEnd { get; } = new(AnalysisEndpointType.LogEnd, null);

    public static AnalysisEndpoint FromMarker(MarkerRecord marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return new AnalysisEndpoint(AnalysisEndpointType.Marker, marker);
    }
}
