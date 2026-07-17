namespace FFXI_LogAnalyzer.Core;

public sealed record MessageTime(
    int Hour,
    int Minute,
    int Second,
    MessageTimePrecision Precision)
{
    public TimeSpan TimeOfDay => new(Hour, Minute, Second);
}
