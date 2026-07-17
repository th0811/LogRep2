using System.Diagnostics;
using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class RealtimeAnalysisEngine
{
    private readonly AnalysisTimeResolver _timeResolver = new();
    private readonly ActionGroupBuilder _groupBuilder = new();
    private readonly ActionGroupParser _groupParser = new(new DefaultAnalysisRuleSet());
    private readonly AnalysisAggregator _aggregator = new();
    private readonly LevelingPointAggregator _pointAggregator = new();

    public RealtimeAnalysisResult Analyze(
        IReadOnlyList<ICanonicalRecord> snapshot,
        int startIndex,
        int endIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var safeStart = Math.Clamp(startIndex, 0, snapshot.Count);
        var safeEnd = Math.Clamp(endIndex, safeStart, snapshot.Count);
        var records = snapshot
            .Skip(safeStart)
            .Take(safeEnd - safeStart)
            .Where(record => !record.IsMarker)
            .ToArray();
        var timer = Stopwatch.StartNew();
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);
        var time = _timeResolver.Resolve(selection, records);
        var parseResults = _groupBuilder.Build(records)
            .Select(group => _groupParser.ParseGroup(group))
            .ToArray();
        var parsed = parseResults
            .Where(result => result.Parsed is not null)
            .Select(result => result.Parsed!)
            .ToArray();
        var unparsed = parseResults
            .Where(result => result.Unparsed is not null)
            .Select(result => result.Unparsed!)
            .ToArray();
        var result = _aggregator.Aggregate(parsed, time, unparsed) with
        {
            LevelingPointSummaries = _pointAggregator.Aggregate(records, time),
        };
        timer.Stop();
        return new RealtimeAnalysisResult(result, records.Length, timer.Elapsed);
    }
}

public sealed record RealtimeAnalysisResult(
    AnalysisResult Result,
    int TargetRecordCount,
    TimeSpan Elapsed);
