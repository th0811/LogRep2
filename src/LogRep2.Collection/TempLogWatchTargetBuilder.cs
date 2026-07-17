using System.Globalization;

namespace FfxiTempLogCollector.Core;

public sealed class TempLogWatchTargetBuilder
{
    public const int MaximumRotationSlots = 20;

    public IReadOnlyList<string> Build(
        string tempDirectory,
        CollectorConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempDirectory);
        ArgumentNullException.ThrowIfNull(config);

        if (config.RotationSlots is < 0 or > MaximumRotationSlots)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                config.RotationSlots,
                $"ローテーション数は0から{MaximumRotationSlots}の範囲で指定してください。");
        }

        var targets = new List<string>(config.RotationSlots * 2);

        if (config.WatchWindow1)
        {
            AddWindowTargets(
                targets,
                tempDirectory,
                windowId: 1,
                config.RotationSlots);
        }

        if (config.WatchWindow2)
        {
            AddWindowTargets(
                targets,
                tempDirectory,
                windowId: 2,
                config.RotationSlots);
        }

        return targets;
    }

    private static void AddWindowTargets(
        ICollection<string> targets,
        string tempDirectory,
        int windowId,
        int rotationSlots)
    {
        for (var rotationIndex = 0; rotationIndex < rotationSlots; rotationIndex++)
        {
            var fileName = string.Create(
                CultureInfo.InvariantCulture,
                $"{windowId}_{rotationIndex}.log");

            targets.Add(Path.Combine(tempDirectory, fileName));
        }
    }
}
