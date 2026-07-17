namespace FfxiTempLogCollector.Core;

public sealed class PollingOptions
{
    public const int MinimumIntervalMs = 250;

    public const int MaximumIntervalMs = 5000;

    public const int DefaultIntervalMs = 1000;

    private int _intervalMs = DefaultIntervalMs;

    public int IntervalMs
    {
        get => Volatile.Read(ref _intervalMs);
        set
        {
            if (value is < MinimumIntervalMs or > MaximumIntervalMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"ポーリング間隔は{MinimumIntervalMs}から{MaximumIntervalMs}ミリ秒の範囲で指定してください。");
            }

            Volatile.Write(ref _intervalMs, value);
        }
    }
}
