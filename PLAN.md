# PLAN: Home Theater Edge Wrapper for Windows

## 1. Project Overview & Objectives
Build a lightweight, borderless Windows 11 desktop wrapper around the Microsoft Edge engine using **Microsoft.Web.WebView2**. The application functions as a dedicated "couch / 10-foot" streaming client optimized for physical TV remotes (specifically via FLIRC USB keyboard emulation) while maintaining hardware-backed **PlayReady SL3000** for native 4K UHD and HDR streaming (Netflix, Prime Video, Plex, YouTube TV).

### Key Goals
* **True Kiosk / Zero Browser Chrome:** No address bars, window borders, title ribbons, context menus, or status indicators.
* **4K PlayReady DRM Compliance:** Pass hardware-acceleration and DRM flags to ensure full 3840×2160 HEVC playback.
* **Seamless FLIRC / Remote Control:** Intercept standard HID keyboard strokes from FLIRC at the application level; bridge D-pad spatial navigation, Back, and Media controls without requiring a mouse.
* **Automated DOM Focus & Cursor Management:** Prevent web Single Page Applications (SPAs) from dropping keyboard focus; auto-hide the mouse cursor after 2 seconds of inactivity.
* **Persistent Session Isolation:** Store logins and cookies in a designated, isolated user data directory.

---

## 2. Recommended Tech Stack
* **Framework:** C# / .NET 10+ with **WPF** (or WinUI 3 / Windows App SDK). *WPF is preferred for simpler low-level window hooks and minimal runtime overhead.*
* **Web Engine:** `Microsoft.Web.WebView2` NuGet package (Official Evergreen runtime).
* **System Utilities:** User32 Win32 APIs for cursor hiding (`ShowCursor`) and raw window activation.

---

## 3. System & Runtime Prerequisites
The host Windows environment must meet the following baseline to achieve 4K HDR playback:
1. **HEVC Codec:** Microsoft Store "HEVC Video Extensions" is already installed.
2. **Display Pipeline:** Single 4K display is active with HDCP 2.2 compliance over HDMI 2.0+ or DP 1.4+.
3. **OS Settings:** Windows HDR is enabled (if using HDR/Dolby Vision).

You don't need to verify these prerequisites.

---

## 4. Architectural Components

### Module A: Window & Shell Management (`MainWindow.xaml` / `.cs`)
* **Window Properties:**
  * `WindowStyle="None"`, `WindowState="Maximized"`, `ResizeMode="NoResize"`, `Background="Black"`, `Topmost="True"`.
* **Cursor Auto-Hider:**
  * Implement an inactivity timer (2 seconds).
  * On mouse move, call `Cursor = Cursors.Arrow;` and restart timer.
  * On timeout or D-Pad keydown event, set `Cursor = Cursors.None;` or use `ShowCursor(false)`.
* **Exit Safeguard:**
  * Bind `Alt + F4` or a designated remote key (e.g., `F12` or `Ctrl + Q`) to invoke `Application.Current.Shutdown()`.

---

### Module B: WebView2 Engine Configuration (`WebViewManager.cs`)
* **Environment Options:**
  Configure `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`:
  ```text
  --enable-features=PlayReadyDrmWin10,PlatformHEVCDecoderSupport,HardwareMediaKeyHandling
  --disable-features=WidevineDrm
  --enable-gpu-rasterization
  --enable-zero-copy

* **User Data Isolation:**
Store browser cache and auth tokens in `%LOCALAPPDATA%\TvKioskWrapper\Profile` to prevent collisions with daily Edge desktop usage.
* **Core Settings:**
* `IsStatusBarEnabled = false`
* `AreDefaultContextMenusEnabled = false`
* `AreDevToolsEnabled = false` (toggleable via `--debug` flag)
* `IsSwipeNavigationEnabled = false`

---

### Module C: Remote Input Handling & Keystroke Normalization (`InputController.cs`)

FLIRC translates IR remote buttons into standard USB keyboard keystrokes. The wrapper will capture these via `PreviewKeyDown` on the root window before passing them down to the WebView2 control:

| Remote Action | FLIRC Emulated Key | App Behavior / Handled Event |
| --- | --- | --- |
| **D-Pad Up/Down/Left/Right** | Arrow Keys (`Up`, `Down`, `Left`, `Right`) | Passed to DOM for spatial selection; hides mouse cursor. |
| **OK / Select** | `Enter` | Passed directly to focused DOM element. |
| **Back** | `BrowserBack` or `Escape` or `Backspace` | If WebView can go back (`CanGoBack`), call `GoBack()`. If at top-level home, show app switcher. |
| **Play / Pause** | `MediaPlayPause` or `Space` | Injects synthetic toggle: `document.querySelector('video')?.paused ? document.querySelector('video')?.play() : document.querySelector('video')?.pause();` |
| **Home / Menu** | `F1` or `Home` | Shows overlay HUD / switches to default streaming service. |
| **Power / Exit** | `F12` | Closes the application. |

---

### Module D: DOM Auto-Focus & Spatial Navigation Injector (`ScriptInjector.cs`)

Streaming SPAs do not process arrow keys until user interaction occurs.

* **On `NavigationCompleted` & `Window.Activated`:**
Automatically execute an initialization script:
1. Call `window.focus(); document.body.focus();`.
2. Dispatch a synthetic centered click event to prime the page's event listeners:
```javascript
(function() {
    const x = window.innerWidth / 2;
    const y = window.innerHeight / 2;
    const el = document.elementFromPoint(x, y);
    if (el) {
        el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, clientX: x, clientY: y }));
    }
    window.focus();
})();

```


3. Inject clean custom CSS to suppress web scrollbars:
```css
::-webkit-scrollbar { display: none !important; }
html, body { scrollbar-width: none !important; }

```

---

### Module E: Optional Service Switcher / HUD Overlay (Phase 2)

A lightweight XAML overlay (semi-transparent bar toggled via the `Menu` or `F1` key) allowing the remote to switch between presets without restarting the application:

* Netflix (`[https://www.netflix.com](https://www.netflix.com)`)
* YouTube on TV (`[https://www.youtube.com/tv](https://www.youtube.com/tv)`)
* Plex Web (`[https://app.plex.tv/desktop/#!/tv](https://app.plex.tv/desktop/#!/tv)`)
* Prime Video (`[https://www.primevideo.com](https://www.primevideo.com)`)

---

## 5. Step-by-Step Implementation Roadmap

### Step 1: Project Scaffolding & Dependencies

* Create a `.NET 10` WPF project named `TvKioskWrapper`.
* Add the NuGet package: `Microsoft.Web.WebView2`.
* Configure `app.manifest` for Windows 10/11 DPI awareness.

### Step 2: Fullscreen Window & Viewport

* Define borderless fullscreen window layout in `MainWindow.xaml`.
* Attach the `WebView2` control to occupy full width/height.
* Wire up mouse inactivity timer to automatically hide `Mouse.OverrideCursor`.

### Step 3: WebView2 Initialization with PlayReady Flags

* Implement asynchronous initialization in `MainWindow.xaml.cs`.
* Configure `CoreWebView2Environment` with custom profile path and PlayReady/HEVC arguments.
* Set initial URL to default service (or configurable via `config.json`).

### Step 4: Focus & Navigation Script Injection

* Subscribe to `NavigationCompleted`.
* Inject the auto-focus and click dispatch scripts.
* Add CSS injection to strip scrollbars.

### Step 5: Input & FLIRC Key Mapping

* Hook into `PreviewKeyDown` at the Window level.
* Add handlers for `BrowserBack`, `MediaPlayPause`, and navigation intercepts.
* Prevent default browser behavior on unwanted keys (e.g., block `F5` reload or `Ctrl+P`).

---

## 6. Verification & Acceptance Criteria

1. **Clean Kiosk Display:**
* App boots into borderless true fullscreen with a solid black backdrop.
* No borders, tabs, scrollbars, or Edge UI elements are visible.
* Cursor vanishes after 2 seconds without mouse input.

2. **DRM & Hardware Decode Verification:**
* Navigate app to `edge://gpu`.
* Under **Media Foundation Rendering Capabilities**, confirm:
* `PlayReady Hardware DRM disabled: false`
* `HEVC installed: true`
* `HDCP 2.2 support: true`

3. **4K Playback Test:**
* Play a 4K title on Netflix.
* Open stats (`Ctrl + Alt + Shift + D`):
* Video resolution reports `3840x2160`.
* Playing codec reports `hevc`.
* KeySystem reports `com.microsoft.playready.recommendation`.

4. **Remote Control Verification:**
* D-pad moves navigation focus across media tiles immediately upon page load without touching a mouse.
* Remote `Select/OK` launches the title.
* Remote `Play/Pause` pauses and resumes video.
* Remote `Back` returns to the previous browse screen.