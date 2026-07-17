namespace FFXI_LogAnalyzer.Core;

public sealed class LoadSessionResult
{
    private LoadSessionResult(
        AnalyzerInputSession? session,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        Session = session;
        Warnings = warnings;
        Errors = errors;
    }

    public AnalyzerInputSession? Session { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsSuccess => Session is not null && Errors.Count == 0;

    public static LoadSessionResult Success(AnalyzerInputSession session, IReadOnlyList<string> warnings)
    {
        return new LoadSessionResult(session, warnings, []);
    }

    public static LoadSessionResult Failure(IReadOnlyList<string> errors)
    {
        return new LoadSessionResult(null, [], errors);
    }
}
