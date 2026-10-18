using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TvKioskWrapper.Configuration;

namespace TvKioskWrapper.Shell;

/// <summary>
/// Manages LG TV display presets via ColorControl.
/// Automatically applies Home Theater mode on startup and wake-from-sleep,
/// and restores PC mode on application shutdown.
/// </summary>
public sealed class ColorControlManager : IDisposable
{
    private readonly ColorControlConfig _config;
    private readonly bool _isAutomation;
    private int _disabled;
    private bool _disposed;

    public ColorControlManager(ColorControlConfig config, bool isAutomation = false)
    {
        _config = config;
        _isAutomation = isAutomation;

        if (_config.Enabled && !_isAutomation)
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
    }

    /// <summary>
    /// Applies the LG TV Home Theater preset asynchronously in the background.
    /// </summary>
    public void EnableHomeTheaterMode()
    {
        if (_isAutomation || !_config.Enabled)
        {
            return;
        }

        Interlocked.Exchange(ref _disabled, 0);
        Task.Run(() => ExecuteCommand(_config.EnableArguments));
    }

    /// <summary>
    /// Restores the LG TV PC preset when quitting.
    /// </summary>
    public void DisableHomeTheaterMode()
    {
        if (_isAutomation || !_config.Enabled)
        {
            return;
        }

        if (Interlocked.Exchange(ref _disabled, 1) != 0)
        {
            return;
        }

        ExecuteCommand(_config.DisableArguments);
    }

    private void ExecuteCommand(string arguments)
    {
        if (_isAutomation || !_config.Enabled)
        {
            return;
        }

        string exePath = _config.ExecutablePath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Debug.WriteLine($"[ColorControl] Executable not found at '{exePath}'.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            using Process? proc = Process.Start(startInfo);
            Debug.WriteLine($"[ColorControl] Executed '{exePath} {arguments}'.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ColorControl] Failed to execute '{exePath} {arguments}': {ex.Message}");
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Debug.WriteLine("[ColorControl] Resumed from sleep; applying Home Theater mode.");
            EnableHomeTheaterMode();

            // Re-apply after 5 seconds to ensure network and display connections are fully ready
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                EnableHomeTheaterMode();
            });
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        DisableHomeTheaterMode();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_config.Enabled && !_isAutomation)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }
}

