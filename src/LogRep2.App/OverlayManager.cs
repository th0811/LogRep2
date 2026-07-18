using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LogRep2.Infrastructure;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FfxiTempLogCollector.App;

public sealed class OverlayManager : IDisposable
{
    private readonly LogRep2SettingsStore _settingsStore;
    private readonly RealtimeAnalysisController _realtimeAnalysis;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _reportError;
    private readonly DispatcherTimer _saveTimer;
    private OverlaySettings _settings;
    private OverlayWindow? _window;
    private OverlayViewModel? _viewModel;
    private bool _subscribed;
    private bool _applyingPlacement;
    private bool _disposed;

    public OverlayManager(
        LogRep2SettingsStore settingsStore,
        RealtimeAnalysisController realtimeAnalysis,
        Dispatcher dispatcher,
        Action<string> reportError)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _realtimeAnalysis = realtimeAnalysis ?? throw new ArgumentNullException(nameof(realtimeAnalysis));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
        _settings = _settingsStore.Load().Overlay;
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, OnSaveTimerTick, dispatcher)
        {
            IsEnabled = false,
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler? VisibilityChanged;

    public bool IsVisible => _window?.IsVisible == true;

    internal bool IsRealtimeUpdateSubscribed => _subscribed;

    public void RestoreConfiguredVisibility()
    {
        if (_settings.Enabled)
        {
            Show();
        }
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        ThrowIfDisposed();
        try
        {
            EnsureWindow();
            ApplyPlacement(reset: false);
            Subscribe();
            _viewModel!.Apply(_realtimeAnalysis.Current);
            _window!.Show();
            _window.Activate();
            _settings.Enabled = true;
            ScheduleSave();
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            HideInternal();
            _settings.Enabled = false;
            SaveNow();
            _reportError($"オーバーレイを表示できませんでした: {exception.Message}");
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Hide()
    {
        ThrowIfDisposed();
        HideInternal();
        _settings.Enabled = false;
        SaveNow();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetPosition()
    {
        ThrowIfDisposed();
        ApplyPlacement(reset: true);
        ScheduleSave();
    }

    public void UpdatePartyMembers(IEnumerable<string> partyMembers)
    {
        _viewModel?.SetPartyMembers(partyMembers);
        _viewModel?.Apply(_realtimeAnalysis.Current);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Unsubscribe();
        _saveTimer.Stop();
        if (_window is not null)
        {
            CaptureWindowSettings();
            _window.LocationChanged -= OnWindowBoundsChanged;
            _window.SizeChanged -= OnWindowBoundsChanged;
            _window.SourceInitialized -= OnSourceInitialized;
            _window.DpiChanged -= OnDpiChanged;
            _window.CloseForShutdown();
            _window = null;
        }

        SaveNow();
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        var partyMembers = _settingsStore.Load().Analysis.RealtimePartyMembers;
        _viewModel = new OverlayViewModel(_settings, partyMembers, Hide, ScheduleSave);
        _window = new OverlayWindow
        {
            DataContext = _viewModel,
        };
        _window.LocationChanged += OnWindowBoundsChanged;
        _window.SizeChanged += OnWindowBoundsChanged;
        _window.SourceInitialized += OnSourceInitialized;
        _window.DpiChanged += OnDpiChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        ApplyPlacement(reset: false);
    }

    private void OnDpiChanged(object sender, System.Windows.DpiChangedEventArgs eventArgs)
    {
        ApplyPlacement(reset: false);
        ScheduleSave();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (IsVisible)
            {
                ApplyPlacement(reset: false);
                ScheduleSave();
            }
        });
    }

    private void ApplyPlacement(bool reset)
    {
        var workAreas = GetWorkAreas();
        if (reset)
        {
            OverlayPlacementService.Reset(_settings, workAreas);
        }
        else
        {
            OverlayPlacementService.Normalize(_settings, workAreas);
        }

        if (_window is null)
        {
            return;
        }

        _applyingPlacement = true;
        try
        {
            _window.Left = _settings.Left;
            _window.Top = _settings.Top;
            _window.Width = _settings.Width;
            _window.Height = _settings.Height;
        }
        finally
        {
            _applyingPlacement = false;
        }
    }

    private IReadOnlyList<OverlayWorkArea> GetWorkAreas()
    {
        var dpiScale = GetDpiScale();
        return Forms.Screen.AllScreens
            .Select(screen => new OverlayWorkArea(
                screen.DeviceName,
                new Rect(
                    screen.WorkingArea.Left / dpiScale,
                    screen.WorkingArea.Top / dpiScale,
                    screen.WorkingArea.Width / dpiScale,
                    screen.WorkingArea.Height / dpiScale),
                screen.Primary))
            .ToArray();
    }

    private double GetDpiScale()
    {
        if (_window is null)
        {
            return 1;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return 1;
        }

        return GetDpiForWindow(handle) / 96d;
    }

    private void OnWindowBoundsChanged(object? sender, EventArgs eventArgs)
    {
        if (!_applyingPlacement)
        {
            CaptureWindowSettings();
            ScheduleSave();
        }
    }

    private void CaptureWindowSettings()
    {
        if (_window is null || _window.WindowState != WindowState.Normal)
        {
            return;
        }

        _settings.Left = _window.Left;
        _settings.Top = _window.Top;
        _settings.Width = _window.ActualWidth;
        _settings.Height = _window.ActualHeight;
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle != IntPtr.Zero)
        {
            _settings.MonitorDeviceName = Forms.Screen.FromHandle(handle).DeviceName;
        }
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _realtimeAnalysis.Updated += OnRealtimeAnalysisUpdated;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _realtimeAnalysis.Updated -= OnRealtimeAnalysisUpdated;
        _subscribed = false;
    }

    private void OnRealtimeAnalysisUpdated(object? sender, RealtimeAnalysisSnapshot snapshot)
    {
        if (!IsVisible)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (IsVisible)
                {
                    _viewModel?.Apply(snapshot);
                }
            }
            catch (Exception exception)
            {
                _reportError($"オーバーレイの更新に失敗しました: {exception.Message}");
            }
        });
    }

    private void HideInternal()
    {
        Unsubscribe();
        _window?.Hide();
    }

    private void ScheduleSave()
    {
        if (_disposed)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs eventArgs)
    {
        _saveTimer.Stop();
        CaptureWindowSettings();
        SaveNow();
    }

    private void SaveNow()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.Overlay = _settings;
            _settingsStore.Save(settings);
        }
        catch (Exception exception)
        {
            _reportError($"オーバーレイ設定を保存できませんでした: {exception.Message}");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);
}
