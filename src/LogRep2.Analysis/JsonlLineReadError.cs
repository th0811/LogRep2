namespace FFXI_LogAnalyzer.Core;

public sealed record JsonlLineReadError(
    int LineNumber,
    string LineText,
    string Message);
