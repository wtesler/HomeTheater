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
    private const string LgPreset = "--nogui --lgpreset ";

    private const string ModeHomeTheater = "Mode_HomeTheater";
    private const string ModePC = "Mode_PC";

    private const string VideoStandard = "Video_Standard";
    private const string VideoCinemaHome = "Video_CinemaHome";

    private const string MotionOff = "Motion_Off";
    private const string MotionCinematic = "Motion_Cinematic";
    private const string MotionNatural = "Motion_Natural";

    private const string SoundStandard = "Sound_Standard";
    private const string SoundVoice = "Sound_Voice";
    private const string SoundCinema = "Sound_Cinema";
    private const string SoundAI = "Sound_AI";

    private readonly OrderedDictionary<string, string> VideoPresets = new()
    {
        { "Standard", VideoStandard },
        { "Cinema Home", VideoCinemaHome }
    };

    private readonly OrderedDictionary<string, string> MotionPresets = new()
    {
        { "Off", MotionOff },
        { "Cinematic", MotionCinematic },
        { "Natural", MotionNatural }
    };

    private readonly OrderedDictionary<string, string> SoundPresets = new()
    {
        { "Standard", SoundStandard },
        { "Voice", SoundVoice },
        { "Cinema", SoundCinema },
        { "AI", SoundAI }
    };

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
        Task.Run(() => ExecuteCommand(LgPreset + ModeHomeTheater));
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

        ExecuteCommand(LgPreset + ModePC);
    }

    public IReadOnlyList<string> VideoPresetNames => [.. VideoPresets.Keys];

    /// <summary>
    /// Applies a specific video preset by index.
    /// </summary>
    public string SetVideoPreset(int index)
    {
        if (_isAutomation || !_config.Enabled)
        {
            return string.Empty;
        }

        if (index < 0 || index >= VideoPresets.Count)
        {
            return string.Empty;
        }

        var (presetName, presetValue) = VideoPresets.GetAt(index);
        ExecuteCommand(LgPreset + presetValue);

        return presetName;
    }

    public IReadOnlyList<string> MotionPresetNames => [.. MotionPresets.Keys];

    /// <summary>
    /// Applies a specific motion preset by index.
    /// </summary>
    public string SetMotionPreset(int index)
    {
        if (_isAutomation || !_config.Enabled)
        {
            return string.Empty;
        }

        if (index < 0 || index >= MotionPresets.Count)
        {
            return string.Empty;
        }

        var (presetName, presetValue) = MotionPresets.GetAt(index);
        ExecuteCommand(LgPreset + presetValue);

        return presetName;
    }

    public IReadOnlyList<string> SoundPresetNames => [.. SoundPresets.Keys];

    /// <summary>
    /// Applies a specific sound preset by index.
    /// </summary>
    public string SetSoundPreset(int index)
    {
        if (_isAutomation || !_config.Enabled)
        {
            return string.Empty;
        }

        if (index < 0 || index >= SoundPresets.Count)
        {
            return string.Empty;
        }

        var (presetName, presetValue) = SoundPresets.GetAt(index);
        ExecuteCommand(LgPreset + presetValue);

        return presetName;
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

