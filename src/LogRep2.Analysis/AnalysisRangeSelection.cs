namespace FFXI_LogAnalyzer.Core;

public sealed record AnalysisRangeSelection(
    AnalysisEndpoint Start,
    AnalysisEndpoint End);
