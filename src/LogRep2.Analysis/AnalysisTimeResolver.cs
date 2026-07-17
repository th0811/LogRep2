using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisTimeResolver
{
    private static readonly DateTimeOffset BaseDate = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MessageTimeParser _messageTimeParser;

    public AnalysisTimeResolver()
        : this(new MessageTimeParser())
    {
    }

    public AnalysisTimeResolver(MessageTimeParser messageTimeParser)
    {
        _messageTimeParser = messageTimeParser;
    }

    public AnalysisTimeResult Resolve(
        AnalysisRangeSelection selection,
        IReadOnlyList<ICanonicalRecord> analysisRecords)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(analysisRecords);

        var warnings = new List<string>();
        var start = ResolveStart(selection.Start, analysisRecords);
        var end = ResolveEnd(selection.End, analysisRecords);

        if (start is null || end is null)
        {
            warnings.Add("分析開始時刻または終了時刻を確定できません。");
            return AnalysisTimeResult.Unknown(warnings);
        }

        var startTime = start.Value.Timestamp;
        var endTime = end.Value.Timestamp;
        if (endTime < startTime)
        {
            endTime = endTime.AddDays(1);
        }

        var durationSeconds = (endTime - startTime).TotalSeconds;
        var confidence = ResolveConfidence(start.Value, end.Value);
        if (durationSeconds <= 0)
        {
            warnings.Add("分析時間が0秒以下のためDPSを算出できません。");
        }

        return new AnalysisTimeResult(confidence, durationSeconds, startTime, endTime, warnings);
    }

    private ResolvedEndpointTime? ResolveStart(
        AnalysisEndpoint endpoint,
        IReadOnlyList<ICanonicalRecord> analysisRecords)
    {
        return endpoint.Type switch
        {
            AnalysisEndpointType.Marker => ResolveMarker(endpoint.Marker, isEnd: false),
            AnalysisEndpointType.LogStart => ResolveFirstRecordTime(analysisRecords),
            _ => null
        };
    }

    private ResolvedEndpointTime? ResolveEnd(
        AnalysisEndpoint endpoint,
        IReadOnlyList<ICanonicalRecord> analysisRecords)
    {
        return endpoint.Type switch
        {
            AnalysisEndpointType.Marker => ResolveMarker(endpoint.Marker, isEnd: true),
            AnalysisEndpointType.LogEnd => ResolveLastRecordTime(analysisRecords),
            _ => null
        };
    }

    private ResolvedEndpointTime? ResolveMarker(MarkerRecord? marker, bool isEnd)
    {
        if (marker is null)
        {
            return null;
        }

        if (_messageTimeParser.TryParse(marker.MessageTimeText, out var messageTime))
        {
            return FromMessageTime(messageTime, isEnd, estimated: false);
        }

        if (marker.FirstSeenAt is { } firstSeenAt)
        {
            return new ResolvedEndpointTime(firstSeenAt, null, Estimated: true);
        }

        return null;
    }

    private ResolvedEndpointTime? ResolveFirstRecordTime(IReadOnlyList<ICanonicalRecord> analysisRecords)
    {
        foreach (var record in analysisRecords)
        {
            if (_messageTimeParser.TryParse(record.MessageTimeText, out var messageTime))
            {
                return FromMessageTime(messageTime, isEnd: false, estimated: true);
            }
        }

        var firstSeenAt = analysisRecords
            .Select(record => record.FirstSeenAt)
            .FirstOrDefault(timestamp => timestamp is not null);
        return firstSeenAt is null
            ? null
            : new ResolvedEndpointTime(firstSeenAt.Value, null, Estimated: true);
    }

    private ResolvedEndpointTime? ResolveLastRecordTime(IReadOnlyList<ICanonicalRecord> analysisRecords)
    {
        for (var i = analysisRecords.Count - 1; i >= 0; i--)
        {
            if (_messageTimeParser.TryParse(analysisRecords[i].MessageTimeText, out var messageTime))
            {
                return FromMessageTime(messageTime, isEnd: true, estimated: true);
            }
        }

        for (var i = analysisRecords.Count - 1; i >= 0; i--)
        {
            var timestamp = analysisRecords[i].LastSeenAt ?? analysisRecords[i].FirstSeenAt;
            if (timestamp is not null)
            {
                return new ResolvedEndpointTime(timestamp.Value, null, Estimated: true);
            }
        }

        return null;
    }

    private static ResolvedEndpointTime FromMessageTime(MessageTime messageTime, bool isEnd, bool estimated)
    {
        var second = messageTime.Precision == MessageTimePrecision.Minute && isEnd
            ? 59
            : messageTime.Second;
        var timestamp = BaseDate.Add(messageTime.TimeOfDay).AddSeconds(second - messageTime.Second);
        return new ResolvedEndpointTime(timestamp, messageTime.Precision, estimated);
    }

    private static TimeConfidence ResolveConfidence(ResolvedEndpointTime start, ResolvedEndpointTime end)
    {
        if (start.Estimated || end.Estimated)
        {
            return TimeConfidence.Estimated;
        }

        if (start.MessagePrecision == MessageTimePrecision.Second &&
            end.MessagePrecision == MessageTimePrecision.Second)
        {
            return TimeConfidence.Exact;
        }

        return TimeConfidence.Minute;
    }

    private readonly record struct ResolvedEndpointTime(
        DateTimeOffset Timestamp,
        MessageTimePrecision? MessagePrecision,
        bool Estimated);
}
