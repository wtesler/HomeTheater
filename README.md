# Home Theater Kiosk Wrapper (`TvKioskWrapper`)

A lightweight, borderless Windows 10/11 desktop wrapper around the Microsoft Edge engine (**WebView2**) built with **C# / .NET 10 and WPF**.

`TvKioskWrapper` turns a Windows HTPC into a dedicated "10-foot" couch streaming client optimized for physical TV remotes (such as **FLIRC USB** keyboard emulators) while preserving hardware-backed **PlayReady SL3000** for native **4K UHD and HDR** playback on services like Netflix, YouTube on TV, Plex, and Prime Video.

---

## Key Features

- **True Kiosk / Zero Browser Chrome:** Launches into a borderless, topmost fullscreen viewport with a solid black backdrop—no address bar, tabs, scrollbars, context menus, status bars, or popup windows.
- **4K UHD & HDR PlayReady SL3000 Support:** Passes hardware DRM and GPU decode flags (`PlayReadyDrmWin10`, `PlatformHEVCDecoderSupport`, `HardwareMediaKeyHandling`, zero-copy GPU rasterization) while disabling software Widevine fallback (`--disable-features=WidevineDrm`).
- **Native YouTube TV Mode (`youtube.com/tv`):** Dynamically spoofs a Smart TV `User-Agent`, strips Windows Chromium Client-Hint headers (`Sec-CH-UA*`), and masks `navigator.userAgentData` on YouTube TV so `https://www.youtube.com/tv` loads the genuine 10-foot Smart TV leanback UI instead of redirecting to desktop YouTube—while restoring the native Windows Edge `User-Agent` on Netflix and other services.
- **2D Spatial D-Pad Navigation Layer:** Injects a custom 2D geometric spatial navigation engine into standard desktop streaming sites (Netflix, Prime Video, etc.) with a high-contrast 10-foot focus ring (`#__tv_kiosk_focus_ring__`) that steps cleanly across video grids, carousels, and search boxes using only arrow keys and `Enter`.
- **10-Foot Local Launchpad Home Page (`https://kiosk.local/index.html`):** Boots into a local Smart TV launchpad webpage displaying all configured streaming services in a D-Pad navigable grid with ambient lighting and a live clock. Pressing `F1` or `Home` at any time returns to this home page.
- **Automatic Cursor Hiding:** Hides the system and DOM mouse cursor after 2 seconds of inactivity or immediately upon pressing any D-Pad / remote button, and restores it automatically on physical mouse movement.
- **Isolated Persistent Profile:** Stores logins, cookies, and site data in `%LOCALAPPDATA%\TvKioskWrapper\Profile` so sessions persist across restarts without colliding with desktop Edge profiles.

---

## System Prerequisites

1. **OS:** Windows 10 or Windows 11 (x64).
2. **.NET Runtime / SDK:** [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (with Windows Desktop runtime).
3. **WebView2 Runtime:** Microsoft Edge Evergreen WebView2 Runtime (pre-installed on Windows 11).
4. **For 4K UHD & HDR Playback:**
   - **HEVC Video Extensions** installed from the Microsoft Store.
   - Display connected via **HDMI 2.0+** or **DisplayPort 1.4+** with **HDCP 2.2+** compliance.
   - Windows HDR enabled in **Settings → System → Display → Use HDR** (when using an HDR10 / Dolby Vision display).

---

## Quick Start

### Build

```powershell
dotnet build -c Release
```

### Run

```powershell
# Launch into the 10-foot Home Theater Launchpad page
dotnet run --project TvKioskWrapper -c Release

# Launch directly into a specific URL
dotnet run --project TvKioskWrapper -c Release -- --url=https://www.youtube.com/tv

# Launch into edge://gpu to inspect PlayReady DRM & HEVC hardware decode status
dotnet run --project TvKioskWrapper -c Release -- --gpu

# Launch with Chromium DevTools enabled
dotnet run --project TvKioskWrapper -c Release -- --debug
```

---

## Remote & Keyboard Controls (FLIRC Mapping)

Map your physical remote buttons in the FLIRC application to the following standard keyboard keys:

| Remote Button | Keyboard Key(s) | Behavior |
| :--- | :--- | :--- |
| **D-Pad Up / Down / Left / Right** | `Up`, `Down`, `Left`, `Right` | Hides cursor immediately. Navigates app tiles on the Home Page, native TV UIs (`youtube.com/tv`), or the injected 2D Spatial Navigation engine on desktop websites. |
| **OK / Select** | `Enter` | Launches the selected app tile on the Home Page, clicks the focused video/button, or activates/submits search inputs. |
| **Back** | `BrowserBack`, `Escape`, `Backspace` | On native TV apps (`youtube.com/tv`), passes `Escape`/`Backspace` to the app's internal navigation. On desktop sites, navigates back in history or returns to the Home Page if at the top-level page. |
| **Home / Menu** | `F1` or `Home` | Returns to the local 10-foot Home Theater Launchpad (`https://kiosk.local/index.html`). |
| **Play / Pause** | `MediaPlayPause` | Toggles play/pause on the active HTML5 `<video>` element. |
| **Power / Exit** | `F12`, `Alt + F4`, `Ctrl + Q` | Cleanly exits the application and restores the system cursor. |
| **GPU Diagnostics** | `Ctrl + Shift + G` | Navigates to `edge://gpu` to verify PlayReady SL3000 & HEVC hardware support. |
| **Netflix Stream Stats** | `Ctrl + Alt + Shift + D` | Passed through to Netflix to display real-time resolution (`3840x2160`), codec (`hevc`), and DRM (`playready`) stats. |

---

## Configuration (`config.json`)

The application reads `config.json` from the application directory on startup (`TvKioskWrapper/config.json` is automatically copied to the build output directory):

```json
{
  "defaultUrl": "https://kiosk.local/index.html",
  "cursorHideDelaySeconds": 2,
  "debug": false,
  "services": [
    {
      "name": "Netflix",
      "subtitle": "4K UHD & HDR",
      "url": "https://www.netflix.com",
      "accentColor": "#E50914",
      "spatialNavigation": true
    },
    {
      "name": "YouTube on TV",
      "subtitle": "Native 10-Foot 4K HDR UI",
      "url": "https://www.youtube.com/tv",
      "accentColor": "#FF0000",
      "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Cobalt/27.lts.3.1040976-gold (unlike Gecko) v8/13.8.258.54-jit gles Starboard/18, TvKioskWrapper_DESKTOP_x8664_2026/1.0.0 (TvKioskWrapper, Windows)",
      "spatialNavigation": false
    },
    {
      "name": "Plex Web",
      "subtitle": "Home Media Server",
      "url": "https://app.plex.tv/desktop/#!/tv",
      "accentColor": "#E5A00D",
      "spatialNavigation": false
    },
    {
      "name": "Prime Video",
      "subtitle": "Amazon Originals & Movies",
      "url": "https://www.primevideo.com",
      "accentColor": "#00A8E1",
      "spatialNavigation": true
    }
  ]
}
```

### Configuration Fields
- **`defaultUrl`**: Initial URL loaded when the application starts (`https://kiosk.local/index.html` loads the local Launchpad Home Page).
- **`cursorHideDelaySeconds`**: Seconds of mouse inactivity before the cursor is hidden (default: `2`).
- **`debug`**: Enables WebView2 DevTools when set to `true` (can also be enabled via `--debug`).
- **`services`**: List of apps rendered on the Launchpad Home Page:
  - **`userAgent`** *(optional)*: Custom `User-Agent` string applied when navigating to this service (omitted/null restores the default Windows Edge `User-Agent`).
  - **`spatialNavigation`**: Set to `true` for desktop websites that need the injected 2D Spatial D-Pad Navigation engine and focus ring; set to `false` for web apps that already provide a native 10-foot TV interface (`youtube.com/tv`, `app.plex.tv/desktop/#!/tv`).

---

## Project Architecture

```text
w:\CSharpProjects\HomeTheater\
├── HomeTheater.slnx
├── PLAN.md
├── PROGRESS.md
├── README.md
└── TvKioskWrapper\
    ├── Assets\
    │   └── Launcher\
    │       ├── index.html              # Local 10-foot Launchpad Home Page markup
    │       ├── launcher.css            # Smart TV grid styling, card animations & ambient glow
    │       └── launcher.js             # 2D grid D-Pad navigation & WebView2 postMessage launcher
    ├── Configuration\
    │   ├── AppConfig.cs                # Root configuration model & preset lookup
    │   ├── ConfigLoader.cs             # JSON config file reader & CLI flag parser
    │   └── StreamingServicePreset.cs   # Streaming service preset model
    ├── Input\
    │   ├── BrowserShortcutFilter.cs    # Shortcut classifier (blocks F5/Ctrl+P, preserves Netflix stats)
    │   └── InputController.cs          # FLIRC remote keystroke normalization & F1/Home routing
    ├── Interop\
    │   └── NativeMethods.cs            # Win32 user32.dll P/Invoke (ShowCursor, GetCursorPos, SetForegroundWindow)
    ├── Scripts\
    │   ├── DomBootstrapScripts.cs      # Scrollbar CSS, Cobalt 4K60/HDR validator & auto-focus scripts
    │   ├── ScriptInjector.cs           # WebView2 script execution orchestrator
    │   └── SpatialNavigationScript.cs  # 2D geometric D-Pad spatial navigation engine for desktop sites
    ├── Shell\
    │   ├── CursorAutoHider.cs          # 2-second inactivity timer & Win32/WPF/DOM cursor sync
    │   └── IKioskShellActions.cs       # Contract for shell-level actions
    ├── WebView\
    │   ├── LauncherHostManager.cs      # Maps https://kiosk.local to Assets/Launcher & handles launch messages
    │   ├── UserAgentOverrideHandler.cs # Dynamic per-service UA switching & Sec-CH-UA header stripping
    │   └── WebViewManager.cs           # WebView2 environment setup, PlayReady/HEVC flags & navigation
    ├── MainWindow.xaml                 # Pure fullscreen borderless WebView2 shell
    ├── MainWindow.xaml.cs              # Top-level window coordinator
    ├── app.manifest                    # Windows 10/11 PerMonitorV2 DPI awareness manifest
    └── config.json                     # Default kiosk settings and streaming service presets
```

---

## Verifying 4K PlayReady DRM & Hardware Decode

1. Launch the app and press `Ctrl + Shift + G` (or run with `--gpu`) to open `edge://gpu`.
2. Scroll to **Media Foundation Rendering Capabilities** and verify:
   - `PlayReady Hardware DRM disabled`: `false`
   - `HEVC installed`: `true`
   - `HDCP 2.2 support`: `true`
3. Switch to **Netflix** (`F1` → Select Netflix), play a 4K UHD title, and press `Ctrl + Alt + Shift + D` to confirm:
   - **Playing resolution:** `3840x2160`
   - **Video codec:** `hevc`
   - **KeySystem:** `com.microsoft.playready.recommendation`

