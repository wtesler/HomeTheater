using System.Windows.Input;

namespace TvKioskWrapper.Input;

/// <summary>
/// Evaluates keyboard shortcuts to block unwanted browser chrome actions while preserving media diagnostics.
/// </summary>
public static class BrowserShortcutFilter
{
    public static bool IsExitShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.F12 ||
               (key == Key.F4 && modifiers.HasFlag(ModifierKeys.Alt)) ||
               (key == Key.Q && modifiers == ModifierKeys.Control);
    }

    public static bool IsGpuDiagnosticsShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.G && modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
    }

    public static bool IsNetflixStatsShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.D &&
               modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);
    }

    public static bool IsSleepShortcut(Key key)
    {
        return key == Key.F10;
    }

    public static bool IsBlockedBrowserShortcut(Key key, ModifierKeys modifiers)
    {
        // Block function keys that trigger browser actions (F5 reload, F7 caret browsing)
        if (key is Key.F5 or Key.F7)
        {
            return true;
        }

        // Block Ctrl+<key> browser shortcuts (Print, Reload, New Window/Tab, Close Tab, Save, Open, Find)
        if (modifiers == ModifierKeys.Control &&
            key is Key.P or Key.R or Key.N or Key.T or Key.W or Key.S or Key.O or Key.F or Key.G or Key.H or Key.J or Key.U)
        {
            return true;
        }

        return false;
    }
}

