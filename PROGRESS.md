# Implementation Progress (`PROGRESS.md`)

**Last Updated:** 2026-10-17

## Summary

All core architectural modules (Modules A–E), roadmap steps (Steps 1–5), **Smart TV User-Agent + Client-Hint Spoofing** for YouTube TV (`youtube.com/tv`), and a **2D Spatial D-Pad Navigation Engine** for desktop streaming websites have been implemented and verified to build cleanly on **.NET 10 (`net10.0-windows`)** in both `Debug` and `Release` configurations.

---

## Completed Milestones

### Step 1: Project Scaffolding & Dependencies
- [x] Created solution `HomeTheater.slnx` and WPF project `TvKioskWrapper/TvKioskWrapper.csproj` targeting `net10.0-windows`.
- [x] Added NuGet dependency `Microsoft.Web.WebView2` (`v1.0.4258.31`).
- [x] Created and linked `TvKioskWrapper/app.manifest` with Windows 10/11 compatibility GUIDs and `PerMonitorV2` DPI awareness for native 4K UHD scaling.
- [x] Created `TvKioskWrapper/config.json` (copied to output directory) and `TvKioskWrapper/AppConfig.cs` supporting configurable default URL, cursor hide timeout, debug toggle, per-service `userAgent` and `spatialNavigation` settings, and CLI flags (`--debug`, `--gpu`, `--url=<url>`).

### Step 2: Fullscreen Window & Viewport (Module A)
- [x] Configured borderless fullscreen kiosk window in `MainWindow.xaml` (`WindowStyle="None"`, `WindowState="Maximized"`, `ResizeMode="NoResize"`, `Background="Black"`, `Topmost="True"`).
- [x] Embedded native HWND `WebView2` control (`KioskWebView`) with a solid black default background (`DefaultBackgroundColor="Black"`) to prevent white flashes during startup/navigation and preserve hardware MPO support for PlayReady SL3000.
- [x] Implemented `NativeMethods.cs` with `user32.dll` P/Invoke bindings (`ShowCursor`, `GetCursorPos`, `SetForegroundWindow`).
- [x] Implemented 2-second cursor inactivity auto-hider in `MainWindow.xaml.cs` combining Win32 cursor counter normalization, WPF `Mouse.OverrideCursor`, and DOM CSS cursor suppression (`* { cursor: none !important; }`). Restores cursor on physical mouse movement (`>= 2px`) and hides immediately on D-Pad/remote key input.

### Step 3: WebView2 Initialization, PlayReady Flags & Dynamic User-Agent Spoofing (Module B)
- [x] Implemented `WebViewManager.cs` with hardware DRM and GPU acceleration flags:
  - `--enable-features=PlayReadyDrmWin10,PlatformHEVCDecoderSupport,HardwareMediaKeyHandling`
  - `--disable-features=WidevineDrm`
  - `--enable-gpu-rasterization`
  - `--enable-zero-copy`
  - `--autoplay-policy=no-user-gesture-required`
- [x] **Smart TV User-Agent, Client-Hint & 4K60 HDR Unlock (`youtube.com/tv`):**
  - Dynamically switches `CoreWebView2.Settings.UserAgent` per service (`Cobalt/27.lts.3.1040976-gold ... Starboard/18` for **YouTube on TV**), and restores the genuine Windows Edge User-Agent for **Netflix** and other services so PlayReady SL3000 4K DRM remains intact.
  - Emulates Cobalt's `MediaSource.isTypeSupported` extended MIME parameter validation (`width`, `height`, `framerate`, `bitrate`, `eotf`) so YouTube TV's `framerate=9999` sanity probe properly returns `false` instead of Chromium's default `true` (which otherwise causes YouTube TV to distrust `isTypeSupported` and cap streams at 720p30 SDR).
  - Exposes `window.h5vcc.system.getVideoContainerSizeOverride = () => '7680x4320'` so Windows DPI scaling (e.g., 250% scaling reporting `1536x864` CSS pixels) does not cap stream resolution.
  - Intercepts `*://*.youtube.com/*` via `WebResourceRequested` when in Smart TV mode to strip `Sec-CH-UA*` Windows client-hint headers (`Sec-CH-UA-Platform`, `Sec-CH-UA`, etc.).
- [x] Configured isolated persistent user profile directory at `%LOCALAPPDATA%\TvKioskWrapper\Profile`.
- [x] Locked down `CoreWebView2Settings` (`IsStatusBarEnabled = false`, `AreDefaultContextMenusEnabled = false`, `AreDevToolsEnabled = config.Debug`, `IsSwipeNavigationEnabled = false`, `IsZoomControlEnabled = false`).

### Step 4: Focus, Scrollbar & 2D Spatial D-Pad Navigation Injector (Module D)
- [x] Implemented `ScriptInjector.cs`:
  - Registers persistent document-creation script (`AddScriptToExecuteOnDocumentCreatedAsync`) and post-navigation script to inject custom CSS hiding WebKit and standard scrollbars (`::-webkit-scrollbar { display: none !important; }`, `scrollbar-width: none !important`).
  - **2D Spatial D-Pad Navigation Engine (`SpatialNavigationEngineScript`):**
    - Active on desktop web interfaces (`Netflix`, `Prime Video`, desktop `YouTube`) and automatically bypassed on native 10-foot TV UIs (`youtube.com/tv`, `app.plex.tv/desktop/#!/tv`).
    - Discovers and deduplicates primary media cards, thumbnails, search inputs, filter chips, and navigation buttons.
    - Renders a high-visibility 10-foot glowing focus ring (`#__tv_kiosk_focus_ring__`) that smoothly tracks the focused element and auto-hides during fullscreen video playback.
    - Scores candidates in 2D space on `ArrowUp`, `ArrowDown`, `ArrowLeft`, and `ArrowRight` (prioritizing row alignment for horizontal movement and smooth auto-scrolling at viewport edges for infinite-scroll grids).
    - Handles `Enter` to activate cards or focus/submit search inputs, and `Escape`/`ArrowDown` inside search boxes to return cleanly to the media grid.

### Step 5: Input & FLIRC Key Mapping (Module C)
- [x] Implemented `InputController.cs` hooked into `MainWindow.PreviewKeyDown`:
  - **D-Pad (`Up`, `Down`, `Left`, `Right`):** Hides cursor immediately; navigates HUD cards when the HUD overlay is visible, or passes through to WebView2 DOM (native TV UI or injected 2D Spatial Navigation layer) when hidden.
  - **OK / Select (`Enter`):** Activates selected HUD preset when HUD is visible, or passes through to WebView2 DOM.
  - **Back (`BrowserBack`, `Escape`, `Backspace`):** Closes HUD if open; passes `Escape`/`Backspace` directly to native TV SPAs (`youtube.com/tv`) and active text inputs; otherwise navigates back in WebView history (`CanGoBack`) or opens the Service Switcher HUD when at the top-level page.
  - **Play / Pause (`MediaPlayPause`, `Space`):** Injects synthetic `<video>` play/pause toggle into the active page.
  - **Home / Menu (`F1`, `Home`):** Toggles the Service Switcher HUD overlay.
  - **Power / Exit (`F12`, `Alt+F4`, `Ctrl+Q`):** Cleanly shuts down the application and restores system cursor visibility.
  - **Diagnostics (`Ctrl+Shift+G`):** Navigates directly to `edge://gpu` for PlayReady/HEVC verification.
  - **Shortcut Filtering:** Blocks unwanted browser keys (`F3`, `F5`, `F7`, `Ctrl+P`, `Ctrl+R`, `Ctrl+N`, `Ctrl+T`, `Ctrl+W`, `Ctrl+F`, etc.) while explicitly preserving `Ctrl+Alt+Shift+D` for Netflix's 4K stream stats overlay.

### Module E: 10-Foot Service Switcher / HUD Overlay
- [x] Built a collapsible top HUD bar in `MainWindow.xaml` and `MainWindow.xaml.cs` with D-Pad spatial selection and accent-colored cards for:
  - **Netflix** (`https://www.netflix.com`) — Edge UA + 2D Spatial Navigation
  - **YouTube on TV** (`https://www.youtube.com/tv`) — Smart TV UA + Native 10-Foot TV UI
  - **Plex Web** (`https://app.plex.tv/desktop/#!/tv`) — Edge UA + Native TV UI
  - **Prime Video** (`https://www.primevideo.com`) — Edge UA + 2D Spatial Navigation
