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

    [LibraryImport("user32.dll")]
    private static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

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

