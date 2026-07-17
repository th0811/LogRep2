namespace FFXI_LogAnalyzer.Core;

public sealed class CanonicalRecordLoadResult
{
    public CanonicalRecordLoadResult(
        IReadOnlyList<CanonicalRecord> records,
        IReadOnlyList<JsonlLineReadError> lineErrors,
        IReadOnlyList<string> errors)
    {
        Records = records;
        LineErrors = lineErrors;
        Errors = errors;
    }

    public IReadOnlyList<CanonicalRecord> Records { get; }

    public IReadOnlyList<JsonlLineReadError> LineErrors { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsSuccess => Errors.Count == 0;

    public bool HasLineErrors => LineErrors.Count > 0;
}
