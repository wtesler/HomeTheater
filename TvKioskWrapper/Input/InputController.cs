using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Wpf;
using TvKioskWrapper.Scripts;
using TvKioskWrapper.Shell;

namespace TvKioskWrapper.Input;

/// <summary>
/// Module C: Remote Input Handling &amp; FLIRC Keystroke Normalization.
/// Intercepts HID keystrokes at both the Win32 message pump and WPF Window level before WebView2 processes them.
/// </summary>
public sealed class InputController
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private readonly WebView2 _webView;
    private readonly ScriptInjector _scriptInjector;
    private readonly IKioskShellActions _shell;

    public InputController(WebView2 webView, ScriptInjector scriptInjector, IKioskShellActions shell)
    {
        _webView = webView;
        _scriptInjector = scriptInjector;
        _shell = shell;

        // Hook Win32 thread message loop directly so keys like F12 and F1 are caught
        // even when focus is inside the Chromium child HWND.
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
    }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled || (msg.message != WM_KEYDOWN && msg.message != WM_SYSKEYDOWN))
        {
            return;
        }

        int virtualKey = (int)msg.wParam;
        Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
        ModifierKeys modifiers = Keyboard.Modifiers;

        if (BrowserShortcutFilter.IsExitShortcut(key, modifiers))
        {
            handled = true;
            Application.Current?.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
            return;
        }

        if (key is Key.F1 or Key.Home)
        {
            handled = true;
            _shell.HideCursorImmediately();
            _shell.NavigateToHome();
            return;
        }

        if (BrowserShortcutFilter.IsGpuDiagnosticsShortcut(key, modifiers))
        {
            handled = true;
            _shell.NavigateToUrl("edge://gpu");
            return;
        }

        if (BrowserShortcutFilter.IsBlockedBrowserShortcut(key, modifiers))
        {
            handled = true;
        }
    }

    public async void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;

        // 1. Power / Exit safeguards: F12, Alt+F4, or Ctrl+Q
        if (BrowserShortcutFilter.IsExitShortcut(key, modifiers))
        {
            e.Handled = true;
            Application.Current?.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
            return;
        }

        // 2. Diagnostic shortcut for Section 6.2 verification: Ctrl+Shift+G -> edge://gpu
        if (BrowserShortcutFilter.IsGpuDiagnosticsShortcut(key, modifiers))
        {
            e.Handled = true;
            _shell.NavigateToUrl("edge://gpu");
            return;
        }

        // 3. Preserve Netflix 4K diagnostic stats shortcut (Ctrl + Alt + Shift + D)
        if (BrowserShortcutFilter.IsNetflixStatsShortcut(key, modifiers))
        {
            return;
        }

        // 4. Block unwanted default browser shortcuts (F5 reload, Ctrl+P print, etc.)
        if (BrowserShortcutFilter.IsBlockedBrowserShortcut(key, modifiers))
        {
            e.Handled = true;
            return;
        }

        // 5. Home / Menu -> Return to the local Launchpad Home Page (F1 or Home)
        if (key is Key.F1 or Key.Home)
        {
            e.Handled = true;
            _shell.HideCursorImmediately();
            _shell.NavigateToHome();
            return;
        }

        // 6. D-Pad spatial navigation (Up, Down, Left, Right) and OK/Select (Enter)
        if (key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter)
        {
            _shell.HideCursorImmediately();

            // If the Chromium child HWND did not yet own Win32 keyboard focus when this key arrived,
            // transfer native focus into WebView2 immediately AND dispatch this keystroke into the DOM
            // so the very first arrow/Enter press works without requiring Tab first.
            if (!_shell.EnsureBrowserFocus())
            {
                e.Handled = true;
                await _scriptInjector.DispatchNavigationKeyAsync(_webView, key);
            }

            return;
        }

        // 7. Dedicated BrowserBack key
        if (key == Key.BrowserBack)
        {
            _shell.HideCursorImmediately();
            e.Handled = true;

            if (_shell.IsOnHomePage)
            {
                return;
            }

            if (_webView.CanGoBack)
            {
                _webView.GoBack();
            }
            else
            {
                _shell.NavigateToHome();
            }
            return;
        }

        // 8. Escape or Backspace
        if (key is Key.Escape or Key.Back)
        {
            _shell.HideCursorImmediately();

            if (_shell.IsOnHomePage)
            {
                e.Handled = true;
                return;
            }

            // On native TV interfaces (e.g. youtube.com/tv), Escape/Backspace navigates inside the TV SPA itself.
            if (_shell.IsNativeTvSpaActive)
            {
                return;
            }

            // For Backspace on desktop websites, allow normal character deletion if inside a text box.
            if (key == Key.Back && await _scriptInjector.ShouldPassBackKeyToDomAsync(_webView))
            {
                return;
            }

            e.Handled = true;

            // Allow semantic navigator to close open modals, drawers, or overlay players first.
            if (await _scriptInjector.TryHandleDomBackAsync(_webView))
            {
                return;
            }

            if (_webView.CanGoBack)
            {
                _webView.GoBack();
            }
            else
            {
                _shell.NavigateToHome();
            }
            return;
        }

        // 9. Play / Pause (MediaPlayPause)
        if (key == Key.MediaPlayPause)
        {
            _shell.HideCursorImmediately();
            e.Handled = true;
            await _scriptInjector.ToggleMediaPlayPauseAsync(_webView);
        }
    }
}
