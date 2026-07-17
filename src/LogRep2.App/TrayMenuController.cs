using Forms = System.Windows.Forms;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.App;

public sealed class TrayMenuController : IDisposable
{
    private readonly GuiCommandController _controller;
    private readonly Forms.ToolStripMenuItem _startItem;
    private readonly Forms.ToolStripMenuItem _stopItem;
    private readonly Forms.ToolStripMenuItem _overlayItem;
    private bool _disposed;

    public TrayMenuController(GuiCommandController controller)
    {
        _controller = controller
            ?? throw new ArgumentNullException(nameof(controller));

        ContextMenu = new Forms.ContextMenuStrip();
        ContextMenu.Items.Add(
            CreateItem("表示", (_, _) => _controller.ShowWindow()));
        ContextMenu.Items.Add(new Forms.ToolStripSeparator());
        _startItem = CreateItem(
            "収集開始",
            async (_, _) => await StartAsync());
        _stopItem = CreateItem(
            "収集停止",
            async (_, _) => await StopAsync());
        ContextMenu.Items.Add(_startItem);
        ContextMenu.Items.Add(_stopItem);
        ContextMenu.Items.Add(new Forms.ToolStripSeparator());
        ContextMenu.Items.Add(
            CreateItem(
                "出力先を開く",
                (_, _) => _controller.OpenOutputDirectory()));
        ContextMenu.Items.Add(
            CreateItem(
                "過去ログ分析",
                (_, _) => _controller.ShowAnalysis()));
        _overlayItem = CreateItem(
            "オーバーレイ表示",
            (_, _) => _controller.ToggleOverlay());
        ContextMenu.Items.Add(_overlayItem);
        ContextMenu.Items.Add(
            CreateItem("設定", (_, _) => _controller.ShowSettings()));
        ContextMenu.Items.Add(new Forms.ToolStripSeparator());
        ContextMenu.Items.Add(
            CreateItem("終了", (_, _) => _controller.Exit()));

        UpdateStatus(_controller.GetStatus());
        _controller.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
    }

    public Forms.ContextMenuStrip ContextMenu { get; }

    public void UpdateStatus(CollectorStatusSnapshot snapshot)
    {
        _startItem.Enabled = snapshot.Status
            is CollectorStatus.Stopped
            or CollectorStatus.Error;
        _stopItem.Enabled = snapshot.Status
            is CollectorStatus.Starting
            or CollectorStatus.Running;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controller.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
        ContextMenu.Dispose();
    }

    private void OnOverlayVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        _overlayItem.Text = _controller.IsOverlayVisible
            ? "オーバーレイ非表示"
            : "オーバーレイ表示";
    }

    private async Task StartAsync()
    {
        try
        {
            await _controller.StartAsync();
        }
        catch (Exception exception)
        {
            _controller.ShowOperationError(
                "収集を開始できませんでした。",
                exception);
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await _controller.StopAsync();
        }
        catch (Exception exception)
        {
            _controller.ShowOperationError(
                "収集を停止できませんでした。",
                exception);
        }
    }

    private static Forms.ToolStripMenuItem CreateItem(
        string text,
        EventHandler handler)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += handler;
        return item;
    }
}
