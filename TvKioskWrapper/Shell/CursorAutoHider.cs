using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TvKioskWrapper.Interop;

namespace TvKioskWrapper.Shell;

/// <summary>
/// Manages mouse inactivity tracking and synchronizes cursor visibility across Win32, WPF, and the WebView2 DOM.
/// </summary>
public sealed class CursorAutoHider
{
    private readonly Window _window;
    private readonly double _hideDelaySeconds;
    private readonly Func<bool, Task> _onDomCursorVisibilityChanged;
    private readonly DispatcherTimer _watchTimer;

    private NativeMethods.POINT _lastCursorPoint;
    private DateTime _lastMouseMoveUtc;

    public bool IsCursorHidden { get; private set; }

    public CursorAutoHider(
        Window window,
        double hideDelaySeconds,
        Func<bool, Task> onDomCursorVisibilityChanged)
    {
        _window = window;
        _hideDelaySeconds = hideDelaySeconds;
        _onDomCursorVisibilityChanged = onDomCursorVisibilityChanged;

        NativeMethods.GetCursorPos(out _lastCursorPoint);
        _lastMouseMoveUtc = DateTime.UtcNow;

        _watchTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _watchTimer.Tick += OnWatchTick;
    }

    public void Start() => _watchTimer.Start();

    public void StopAndRestore()
    {
        _watchTimer.Stop();
        NativeMethods.SetSystemCursorVisibility(true);
        Mouse.OverrideCursor = null;
    }

    public async void NotifyMouseMoved()
    {
        _lastMouseMoveUtc = DateTime.UtcNow;
        if (!IsCursorHidden)
        {
            return;
        }

        IsCursorHidden = false;
        _window.Cursor = Cursors.Arrow;
        Mouse.OverrideCursor = null;
        NativeMethods.SetSystemCursorVisibility(true);
        await _onDomCursorVisibilityChanged(false);
    }

    public async void HideImmediately()
    {
        _lastMouseMoveUtc = DateTime.UtcNow.AddSeconds(-_hideDelaySeconds - 1);
        if (IsCursorHidden)
        {
            return;
        }

        IsCursorHidden = true;
        _window.Cursor = Cursors.None;
        Mouse.OverrideCursor = Cursors.None;
        NativeMethods.SetSystemCursorVisibility(false);
        await _onDomCursorVisibilityChanged(true);
    }

    private void OnWatchTick(object? sender, EventArgs e)
    {
        if (NativeMethods.GetCursorPos(out NativeMethods.POINT currentPoint))
        {
            int dx = Math.Abs(currentPoint.X - _lastCursorPoint.X);
            int dy = Math.Abs(currentPoint.Y - _lastCursorPoint.Y);
            if (dx >= 2 || dy >= 2)
            {
                _lastCursorPoint = currentPoint;
                NotifyMouseMoved();
                return;
            }
        }

        double elapsedSeconds = (DateTime.UtcNow - _lastMouseMoveUtc).TotalSeconds;
        if (!IsCursorHidden && elapsedSeconds >= _hideDelaySeconds)
        {
            HideImmediately();
        }
    }
}

