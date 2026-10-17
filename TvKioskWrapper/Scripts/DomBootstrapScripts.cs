namespace TvKioskWrapper.Scripts;

/// <summary>
/// Provides JavaScript snippets for scrollbar suppression, Cobalt 4K60/HDR media capability emulation,
/// DOM auto-focus priming, media playback toggling, and cursor visibility.
/// </summary>
public static class DomBootstrapScripts
{
    public static string DocumentCreatedBootstrap =>
        ScriptFileLoader.Load("Scripts/Js/document-bootstrap.js");

    public const string EnsureDomFocus = """
        (function() {
            try {
                window.focus();
                if (document.body && (!document.activeElement || document.activeElement === document.documentElement)) {
                    if (!document.body.hasAttribute('tabindex')) {
                        document.body.setAttribute('tabindex', '-1');
                    }
                    document.body.focus({ preventScroll: true });
                }
            } catch (_) {}
        })();
        """;

    public const string PlayPauseToggle = """
        (function() {
            const video = document.querySelector('video');
            if (video) {
                video.paused ? video.play() : video.pause();
                return true;
            }
            return false;
        })();
        """;

    public const string CheckEditableOrNativeTvSpa = """
        (function() {
            if (window.__TV_KIOSK_DISABLE_SPATIAL_NAV__ === true) {
                return true;
            }
            const el = document.activeElement;
            if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable)) {
                return true;
            }
            return false;
        })();
        """;

    public const string TryHandleSemanticBack = """
        (function() {
            if (typeof window.__tvKioskTryHandleBack === 'function') {
                return window.__tvKioskTryHandleBack() === true;
            }
            return false;
        })();
        """;

    public const string HideDomCursor = """
        (function() {
            const id = '__tv_kiosk_cursor_hide__';
            if (!document.getElementById(id)) {
                const s = document.createElement('style');
                s.id = id;
                s.textContent = '* { cursor: none !important; }';
                (document.head || document.documentElement)?.appendChild(s);
            }
        })();
        """;

    public const string ShowDomCursor = """
        (function() {
            document.getElementById('__tv_kiosk_cursor_hide__')?.remove();
        })();
        """;
}

