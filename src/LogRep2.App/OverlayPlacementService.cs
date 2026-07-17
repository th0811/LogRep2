using System.Windows;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.App;

public sealed record OverlayWorkArea(
    string DeviceName,
    Rect Bounds,
    bool IsPrimary);

public static class OverlayPlacementService
{
    public const double MinimumWidth = 280;
    public const double MinimumHeight = 180;
    public const double DefaultLeft = 100;
    public const double DefaultTop = 100;

    public static void Normalize(
        OverlaySettings settings,
        IReadOnlyList<OverlayWorkArea> workAreas)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(workAreas);

        settings.Opacity = Math.Clamp(settings.Opacity, 0.25, 1.0);
        settings.Width = Math.Max(MinimumWidth, settings.Width);
        settings.Height = Math.Max(MinimumHeight, settings.Height);
        settings.FontSize = Math.Clamp(settings.FontSize, 10, 40);
        settings.DisplayRowCount = Math.Clamp(settings.DisplayRowCount, 1, 30);

        var target = FindTarget(settings.MonitorDeviceName, workAreas);
        if (target is null)
        {
            settings.Left = DefaultLeft;
            settings.Top = DefaultTop;
            settings.MonitorDeviceName = null;
            return;
        }

        var bounds = target.Bounds;
        settings.Width = Math.Min(settings.Width, bounds.Width);
        settings.Height = Math.Min(settings.Height, bounds.Height);
        settings.Left = Math.Clamp(
            settings.Left,
            bounds.Left,
            bounds.Right - settings.Width);
        settings.Top = Math.Clamp(
            settings.Top,
            bounds.Top,
            bounds.Bottom - settings.Height);
        settings.MonitorDeviceName = target.DeviceName;
    }

    public static void Reset(
        OverlaySettings settings,
        IReadOnlyList<OverlayWorkArea> workAreas)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var primary = workAreas.FirstOrDefault(area => area.IsPrimary)
            ?? workAreas.FirstOrDefault();
        settings.Width = 420;
        settings.Height = 300;
        settings.Left = primary?.Bounds.Left + DefaultLeft ?? DefaultLeft;
        settings.Top = primary?.Bounds.Top + DefaultTop ?? DefaultTop;
        settings.MonitorDeviceName = primary?.DeviceName;
        Normalize(settings, workAreas);
    }

    private static OverlayWorkArea? FindTarget(
        string? deviceName,
        IReadOnlyList<OverlayWorkArea> workAreas)
    {
        return workAreas.FirstOrDefault(area => string.Equals(
                   area.DeviceName,
                   deviceName,
                   StringComparison.OrdinalIgnoreCase))
            ?? workAreas.FirstOrDefault(area => area.IsPrimary)
            ?? workAreas.FirstOrDefault();
    }
}
