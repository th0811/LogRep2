using System.Windows;
using FfxiTempLogCollector.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class OverlayPlacementServiceTests
{
    [Fact]
    public void Normalize_切断されたモニター設定をプライマリー画面内へ戻す()
    {
        var settings = new OverlaySettings
        {
            MonitorDeviceName = "切断済み",
            Left = 4000,
            Top = 3000,
            Width = 420,
            Height = 300,
        };
        var workAreas = new[]
        {
            new OverlayWorkArea("PRIMARY", new Rect(0, 0, 1920, 1040), true),
        };

        OverlayPlacementService.Normalize(settings, workAreas);

        Assert.Equal("PRIMARY", settings.MonitorDeviceName);
        Assert.InRange(settings.Left, 0, 1500);
        Assert.InRange(settings.Top, 0, 740);
    }

    [Fact]
    public void Normalize_DPI変換後の作業領域を越えない()
    {
        var settings = new OverlaySettings
        {
            MonitorDeviceName = "DISPLAY2",
            Left = 1800,
            Top = 900,
            Width = 800,
            Height = 600,
        };
        var workAreas = new[]
        {
            new OverlayWorkArea("DISPLAY2", new Rect(1280, 0, 1280, 960), false),
            new OverlayWorkArea("PRIMARY", new Rect(0, 0, 1280, 720), true),
        };

        OverlayPlacementService.Normalize(settings, workAreas);

        Assert.Equal(1760, settings.Left);
        Assert.Equal(360, settings.Top);
    }

    [Fact]
    public void Normalize_透明度と操作可能サイズを安全範囲へ補正する()
    {
        var settings = new OverlaySettings
        {
            Opacity = 0.01,
            Width = 10,
            Height = 20,
            FontSize = 100,
            DisplayRowCount = 100,
        };

        OverlayPlacementService.Normalize(
            settings,
            [new OverlayWorkArea("PRIMARY", new Rect(0, 0, 1920, 1080), true)]);

        Assert.Equal(0.25, settings.Opacity);
        Assert.Equal(280, settings.Width);
        Assert.Equal(180, settings.Height);
        Assert.Equal(40, settings.FontSize);
        Assert.Equal(30, settings.DisplayRowCount);
    }

    [Fact]
    public void Reset_プライマリー画面の既定位置へ戻す()
    {
        var settings = new OverlaySettings { Left = -900, Top = -800 };

        OverlayPlacementService.Reset(
            settings,
            [new OverlayWorkArea("PRIMARY", new Rect(100, 50, 1920, 1080), true)]);

        Assert.Equal(200, settings.Left);
        Assert.Equal(150, settings.Top);
        Assert.Equal(420, settings.Width);
        Assert.Equal(300, settings.Height);
    }
}
