using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TvKioskWrapper.Scripts.Navigators;

namespace TvKioskWrapper.Scripts;

/// <summary>
/// Module D: Orchestrates script injection into WebView2 for scrollbar suppression,
/// Cobalt 4K60/HDR capability emulation, auto-focus priming, and per-app semantic D-Pad navigation.
/// </summary>
public sealed class ScriptInjector
{
    public async Task RegisterPersistentScriptsAsync(CoreWebView2 coreWebView)
    {
        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(DomBootstrapScripts.DocumentCreatedBootstrap);
    }

    public async Task InjectFocusAndScrollbarStylesAsync(
        WebView2 webView,
        bool enableSpatialNavigation = true,
        bool isHomePage = false,
        string? navigatorType = null)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            string flagScript = $"window.__TV_KIOSK_DISABLE_SPATIAL_NAV__ = {(!enableSpatialNavigation).ToString().ToLowerInvariant()};";
            await webView.CoreWebView2.ExecuteScriptAsync(flagScript);

            if (!isHomePage)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(DomBootstrapScripts.EnsureDomFocus);
            }

            if (enableSpatialNavigation && !isHomePage)
            {
                string mode = navigatorType?.ToLowerInvariant() ?? "spatial";
                if (mode == "hbomax")
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(SemanticNavigatorCoreScript.Source);
                    await webView.CoreWebView2.ExecuteScriptAsync(HboMaxNavigatorScript.Source);
                }
                else if (mode != "native")
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(SpatialNavigationScript.Source);
                }
            }
        }
        catch
        {
            // Ignore script errors on internal browser pages (e.g. edge://gpu).
        }
    }

    public async Task DispatchNavigationKeyAsync(WebView2 webView, Key key)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        (string? domKey, int keyCode) = key switch
        {
            Key.Up => ("ArrowUp", 38),
            Key.Down => ("ArrowDown", 40),
            Key.Left => ("ArrowLeft", 37),
            Key.Right => ("ArrowRight", 39),
            Key.Enter => ("Enter", 13),
            Key.Escape => ("Escape", 27),
            Key.Back => ("Backspace", 8),
            _ => (null, 0)
        };

        if (domKey is null)
        {
            return;
        }

        string script = $$"""
            (function() {
                const target = document.activeElement || document.body || window;
                const ev = new KeyboardEvent('keydown', {
                    key: '{{domKey}}',
                    code: '{{domKey}}',
                    keyCode: {{keyCode}},
                    which: {{keyCode}},
                    bubbles: true,
                    cancelable: true
                });
                target.dispatchEvent(ev);
            })();
            """;

        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // Ignore if page is not ready.
        }
    }

    public async Task ToggleMediaPlayPauseAsync(WebView2 webView)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(DomBootstrapScripts.PlayPauseToggle);
        }
        catch
        {
            // Ignore if page blocks script execution.
        }
    }

    public async Task<bool> ShouldPassBackKeyToDomAsync(WebView2 webView)
    {
        if (webView.CoreWebView2 is null)
        {
            return false;
        }

        try
        {
            string result = await webView.CoreWebView2.ExecuteScriptAsync(DomBootstrapScripts.CheckEditableOrNativeTvSpa);
            return string.Equals(result?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryHandleDomBackAsync(WebView2 webView)
    {
        if (webView.CoreWebView2 is null)
        {
            return false;
        }

        try
        {
            string result = await webView.CoreWebView2.ExecuteScriptAsync(DomBootstrapScripts.TryHandleSemanticBack);
            return string.Equals(result?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task SetDomCursorHiddenAsync(WebView2 webView, bool hidden)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        string script = hidden ? DomBootstrapScripts.HideDomCursor : DomBootstrapScripts.ShowDomCursor;
        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // Ignore if page is not ready.
        }
    }
}

