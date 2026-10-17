using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using TvKioskWrapper.Interop;

namespace TvKioskWrapper.Shell;

/// <summary>
/// Launches native desktop applications (such as Plex HTPC) in front of the kiosk window,
/// yields foreground control to them, and intercepts global F1/F12 hotkeys to close the
/// external app and return to the Launchpad.
/// </summary>
public sealed class ExternalAppManager : IDisposable
{
    private const int HotKeyIdHomeF1 = 9001;
    private const int HotKeyIdExitF12 = 9002;

    private readonly Window _ownerWindow;
    private HwndSource? _hwndSource;
    private Process? _activeProcess;
    private string? _activeProcessName;
    private bool _hotKeysRegistered;

    public bool IsAppRunning => _activeProcess is { HasExited: false };

    public event Action? ReturnToLauncherRequested;

    public ExternalAppManager(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;
    }

    public bool Launch(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        EnsureWindowHook();
        RegisterGlobalHotKeys();

        string processName = Path.GetFileNameWithoutExtension(executablePath);
        _activeProcessName = processName;

        // Reuse an already-running instance if present; otherwise start a new process.
        Process? existing = Process.GetProcessesByName(processName).FirstOrDefault(p => !p.HasExited);
        if (existing is not null)
        {
            AttachProcess(existing);
            _ = BringProcessToFrontAsync(existing);
            return true;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true
        };

        Process? started = Process.Start(startInfo);
        if (started is null)
        {
            UnregisterGlobalHotKeys();
            return false;
        }

        AttachProcess(started);
        _ = BringProcessToFrontAsync(started);
        return true;
    }

    public void CloseActiveApp()
    {
        UnregisterGlobalHotKeys();

        Process? proc = _activeProcess;
        string? procName = _activeProcessName;
        _activeProcess = null;
        _activeProcessName = null;

        if (proc is null && string.IsNullOrWhiteSpace(procName))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            var targets = new List<Process>();
            if (proc is not null)
            {
                targets.Add(proc);
            }
            if (!string.IsNullOrWhiteSpace(procName))
            {
                foreach (Process p in Process.GetProcessesByName(procName))
                {
                    if (targets.All(t => t.Id != p.Id))
                    {
                        targets.Add(p);
                    }
                }
            }

            foreach (Process target in targets)
            {
                try
                {
                    if (!target.HasExited)
                    {
                        target.CloseMainWindow();
                    }
                }
                catch
                {
                    // Ignore if process already exited.
                }
            }

            await Task.Delay(350);

            foreach (Process target in targets)
            {
                try
                {
                    if (!target.HasExited)
                    {
                        target.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore access or already-exited errors.
                }
                finally
                {
                    target.Dispose();
                }
            }
        });
    }

    private void AttachProcess(Process process)
    {
        _activeProcess = process;
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += OnActiveProcessExited;
        }
        catch
        {
            // Ignore if process exits immediately or denies event registration.
        }
    }

    private void OnActiveProcessExited(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _activeProcess))
        {
            return;
        }

        _activeProcess = null;
        _activeProcessName = null;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            UnregisterGlobalHotKeys();
            ReturnToLauncherRequested?.Invoke();
        });
    }

    private static async Task BringProcessToFrontAsync(Process process)
    {
        for (int i = 0; i < 25; i++)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Refresh();
            nint hwnd = process.MainWindowHandle;
            if (hwnd != nint.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
                return;
            }

            await Task.Delay(200);
        }
    }

    private void EnsureWindowHook()
    {
        if (_hwndSource is not null)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(_ownerWindow).Handle;
        if (hwnd != nint.Zero)
        {
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }
    }

    private void RegisterGlobalHotKeys()
    {
        if (_hotKeysRegistered)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(_ownerWindow).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        NativeMethods.RegisterHotKey(hwnd, HotKeyIdHomeF1, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F1);
        NativeMethods.RegisterHotKey(hwnd, HotKeyIdExitF12, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F12);
        _hotKeysRegistered = true;
    }

    private void UnregisterGlobalHotKeys()
    {
        if (!_hotKeysRegistered)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(_ownerWindow).Handle;
        if (hwnd != nint.Zero)
        {
            NativeMethods.UnregisterHotKey(hwnd, HotKeyIdHomeF1);
            NativeMethods.UnregisterHotKey(hwnd, HotKeyIdExitF12);
        }

        _hotKeysRegistered = false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HotKeyIdHomeF1)
            {
                handled = true;
                CloseActiveApp();
                ReturnToLauncherRequested?.Invoke();
            }
            else if (id == HotKeyIdExitF12)
            {
                handled = true;
                CloseActiveApp();
                Application.Current?.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
            }
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        CloseActiveApp();
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}

