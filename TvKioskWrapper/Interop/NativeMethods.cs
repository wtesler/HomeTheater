using System.Runtime.InteropServices;

namespace TvKioskWrapper.Interop;

/// <summary>
/// Win32 User32 P/Invoke utilities for cursor hiding, physical cursor polling, and raw window activation.
/// </summary>
internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    internal const int WM_HOTKEY = 0x0312;
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const uint VK_F1 = 0x70;
    internal const uint VK_F2 = 0x71;
    internal const uint VK_F3 = 0x72;
    internal const uint VK_F4 = 0x73;
    internal const uint VK_F12 = 0x7B;
    internal const int SW_RESTORE = 9;

    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    internal static partial int GetWindowLong(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    internal static partial int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport("ole32.dll")]
    internal static partial int PropVariantClear(ref AudioInterop.PropVariant pvar);

    [LibraryImport("user32.dll")]
    private static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    /// <summary>
    /// Normalizes the Win32 thread cursor display counter so the cursor is deterministically shown or hidden,
    /// including over child HWNDs such as WebView2.
    /// </summary>
    internal static void SetSystemCursorVisibility(bool visible)
    {
        const int maxIterations = 32;
        if (visible)
        {
            for (int i = 0; i < maxIterations && ShowCursor(true) < 0; i++)
            {
            }
        }
        else
        {
            for (int i = 0; i < maxIterations && ShowCursor(false) >= 0; i++)
            {
            }
        }
    }
}

