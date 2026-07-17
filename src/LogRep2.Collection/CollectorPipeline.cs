namespace FfxiTempLogCollector.Core;

public sealed class CollectorPipeline
{
    private readonly TempLogFileNameParser _fileNameParser;
    private readonly TempLogFileParser _fileParser;
    private readonly RecordDecoder _recordDecoder;
    private readonly TimestampExtractor _timestampExtractor;
    private readonly MarkerDetector _markerDetector;
    private readonly RawRecordFactory _rawRecordFactory;
    private readonly RawRecordJsonlWriter _rawWriter;

    public CollectorPipeline(
        TempLogFileNameParser? fileNameParser = null,
        TempLogFileParser? fileParser = null,
        RecordDecoder? recordDecoder = null,
        TimestampExtractor? timestampExtractor = null,
        MarkerDetector? markerDetector = null,
        RawRecordFactory? rawRecordFactory = null,
        RawRecordJsonlWriter? rawWriter = null)
    {
        _fileNameParser = fileNameParser ?? new TempLogFileNameParser();
        _fileParser = fileParser ?? new TempLogFileParser();
        _recordDecoder = recordDecoder ?? new RecordDecoder();
        _timestampExtractor = timestampExtractor ?? new TimestampExtractor();
        _markerDetector = markerDetector ?? new MarkerDetector();
        _rawRecordFactory = rawRecordFactory ?? new RawRecordFactory();
        _rawWriter = rawWriter ?? new RawRecordJsonlWriter();
    }

    public void Process(
        FileSnapshot snapshot,
        string sessionId,
        string sessionDirectory,
        CollectorConfig config,
        RawDeduplicator rawDeduplicator,
        CanonicalDeduplicator canonicalDeduplicator,
        CollectorStats stats,
        DateTimeOffset firstSeenAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rawDeduplicator);
        ArgumentNullException.ThrowIfNull(canonicalDeduplicator);
        ArgumentNullException.ThrowIfNull(stats);

        if (!_fileNameParser.TryParse(
                snapshot.FileName,
                out var parsedFileName)
            || parsedFileName is null)
        {
            return;
        }

        var parsedFile = _fileParser.Parse(snapshot.Content);

        if (parsedFile.ParseStatus == ParseStatus.Error)
        {
            stats.ParseErrors++;
        }

        foreach (var parsedRecord in parsedFile.Records)
        {
            var decodedMessage = _recordDecoder.Decode(parsedRecord);
            var timestamp = _timestampExtractor.Extract(
                decodedMessage.VisibleText);
            var marker = config.MarkerDetection
                ? _markerDetector.Detect(
                    decodedMessage.VisibleText,
                    config.MarkerPrefix)
                : null;
            var rawRecord = _rawRecordFactory.Create(
                new RawRecordContext
                {
                    SessionId = sessionId,
                    FirstSeenAt = firstSeenAt,
                    SourceFile = snapshot.FileName,
                    WindowId = parsedFileName.WindowId,
                    RotationIndex = parsedFileName.RotationIndex,
                    FileMtime = snapshot.LastWriteTime,
                    FileSize = snapshot.FileSize,
                    FileHash = snapshot.FileHash,
                },
                parsedRecord,
                decodedMessage,
                timestamp,
                marker);

            if (parsedRecord.ParseStatus == ParseStatus.Error)
            {
                stats.ParseErrors++;
            }

            var isNewRawRecord = !config.DedupeRaw
                || rawDeduplicator.TryAdd(rawRecord);

            if (!isNewRawRecord)
            {
                stats.DuplicateRawRecordsSkipped++;
                continue;
            }

            if (config.RawOutput)
            {
                _rawWriter.Append(sessionDirectory, rawRecord);
                stats.RawRecordsWritten++;
            }

            if (rawRecord.ParseStatus != ParseStatus.Success)
            {
                continue;
            }

            var canonicalCountBefore = canonicalDeduplicator.Records.Count;
            canonicalDeduplicator.AddOrMerge(rawRecord);

            if (canonicalDeduplicator.Records.Count == canonicalCountBefore)
            {
                stats.DuplicateCanonicalRecordsSkipped++;
            }

            stats.LastSeenAt = firstSeenAt;
        }
    }
}
