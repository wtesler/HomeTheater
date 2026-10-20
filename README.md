# Home Theater Kiosk Wrapper (`TvKioskWrapper`)

A lightweight, borderless Windows 10/11 desktop wrapper around the Microsoft Edge engine (**WebView2**) built with **C# / .NET 10 and WPF**.

`TvKioskWrapper` turns a Windows HTPC into a dedicated "10-foot" couch streaming client optimized for physical TV remotes (such as **FLIRC USB** keyboard emulators) while preserving hardware-backed **PlayReady SL3000** for native **4K UHD and HDR** playback on services like HBO Max, YouTube on TV, Plex HTPC, and Prime Video.

---

## Key Features

- **True Kiosk / Zero Browser Chrome:** Launches into a borderless, topmost fullscreen viewport with a solid black backdrop—no address bar, tabs, scrollbars, context menus, status bars, or popup windows.
- **4K UHD & HDR PlayReady SL3000 Support:** Passes hardware DRM and GPU decode flags (`PlayReadyDrmWin10`, `PlatformHEVCDecoderSupport`, `HardwareMediaKeyHandling`, zero-copy GPU rasterization) while disabling software Widevine fallback (`--disable-features=WidevineDrm`).
- **Tailored Semantic D-Pad Navigators:** Injects a shared semantic navigation core (`semantic-navigator-core.js`) paired with per-app adapters (`hbomax-navigator.js`, `primevideo-navigator.js`) that understand each streaming site's DOM landmarks (Top Nav, Hero Carousels, Content Rails, Detail Actions, Season Selectors, Episode Grids, and Search) with a high-contrast 10-foot focus ring (`#__tv_kiosk_focus_ring__`).
- **D-Pad On-Screen Keyboard:** Automatically pops up a 10-foot on-screen keyboard (`on-screen-keyboard.js`) whenever a search bar or text input is focused in web apps (HBO Max, Prime Video). Supports full D-Pad navigation, number/letter rows, symbols (`?123`), shift, space, backspace, clear, `Go ↵` form submission, and closes cleanly via remote `Back` / `Escape`.
- **10-Foot Settings Popup Menu (`F2`):** Displays a sleek top-left popup menu with drilldown context menus to configure **Video** picture presets (Standard and Cinema Home), **Audio** output devices (enumerating active Windows playback endpoints with live default detection), **Clarity** motion presets (Off, Cinematic, Natural), and **System** actions (Force Home Theater mode) using remote D-Pad controls or mouse clicks. Operates seamlessly both in the kiosk and on top of external desktop applications (such as Plex HTPC).
- **Native YouTube TV Mode (`youtube.com/tv`):** Dynamically spoofs a Smart TV `User-Agent`, strips Windows Chromium Client-Hint headers (`Sec-CH-UA*`), and masks `navigator.userAgentData` on YouTube TV so `https://www.youtube.com/tv` loads the genuine 10-foot Smart TV leanback UI instead of redirecting to desktop YouTube.
- **Native Desktop App Launching (`executablePath`):** Supports launching native 10-foot desktop applications such as **Plex HTPC** in front of the kiosk while intercepting global `F1` / `F12` hotkeys (`RegisterHotKey`) so pressing `F1` closes the external app and returns seamlessly to the Launchpad.
- **10-Foot Local Launchpad Home Page (`https://launchpad.example/index.html`):** Boots into a local Smart TV launchpad webpage displaying all configured streaming services in a D-Pad navigable grid with ambient lighting and a live clock. Pressing `F1` or `Home` at any time returns to this home page.
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
dotnet build
```

### Run

```powershell
# Launch into the 10-foot Home Theater Launchpad page
dotnet run --project TvKioskWrapper

# Launch directly into a specific URL
dotnet run --project TvKioskWrapper -- --url=https://www.youtube.com/tv

# Launch into edge://gpu to inspect PlayReady DRM & HEVC hardware decode status
dotnet run --project TvKioskWrapper -- --gpu

# Launch with Chromium DevTools enabled
dotnet run --project TvKioskWrapper -- --debug
```

---

## UI Automation Tests (`TvKioskWrapper.Tests`)

The solution includes an xUnit UI Automation test project (`TvKioskWrapper.Tests`) that boots a single background `TvKioskWrapper.exe --automation` instance (positioned offscreen without stealing focus) and verifies each app navigator script (`hbomax`, `primevideo`) using compact JSON status assertions.

Use `--nologo -v q` for quiet, token-efficient output (prints a single summary line when passing):

```powershell
# Run all tests at once (app navigators, on-screen keyboard, and settings menu)
dotnet test --nologo -v q

# Run only the settings popup menu tests
dotnet test --nologo -v q --filter SettingsMenuTests

# Run a single app navigator test by class name
dotnet test --nologo -v q --filter HboMax
dotnet test --nologo -v q --filter PrimeVideo

# Run on-screen keyboard tests
dotnet test --nologo -v q --filter Feature=osk

# Or filter by the Navigator trait
dotnet test --nologo -v q --filter Navigator=hbomax
dotnet test --nologo -v q --filter Navigator=primevideo
```

### What Each Test Verifies
1. **App Navigators (`HboMaxNavigatorTests`, `PrimeVideoNavigatorTests`):**
   - Navigates to the streaming service URL and verifies the expected semantic navigator adapter (`hbomax` or `primevideo`) mounts onto `semantic-navigator-core.js`.
   - Confirms semantic rows (`top-nav`, `hero`, `rail`, etc.) are populated and initial focus is placed on a visible element.
   - Simulates D-Pad navigation (`Up` to `top-nav`, `Right` across navigation links, and `Down` back into main content) and verifies focus transitions cleanly.
2. **On-Screen Keyboard (`OnScreenKeyboardTests`):**
   - Focuses search inputs on Prime Video and HBO Max, verifying the on-screen keyboard mounts and becomes visible.
   - Tests D-Pad key selection and character input via `Enter` (including character concatenation).
   - Verifies `Escape` (or remote `Back`) dismisses the keyboard while preserving typed text.
3. **Settings Menu (`SettingsMenuTests`):**
   - Verifies D-Pad navigation across the top-level categories ("Video", "Audio", "Clarity", "System").
   - Confirms drilldown context menus open on `Enter` or `Right` arrow.
   - Tests video preset selection ("Standard", "Cinema Home"), motion clarity preset selection ("Off", "Cinematic", "Natural"), and dynamic audio device selection with active default detection.
   - Tests "Force Home Theater" system action execution.
   - Verifies submenu back-navigation (`Left` arrow, `Back`, `Escape`) and `F2` toggle dismiss.

---

## Remote & Keyboard Controls (FLIRC Mapping)

Map your physical remote buttons in the FLIRC application to the following standard keyboard keys:

| Remote Button | Keyboard Key(s) | Behavior |
| :--- | :--- | :--- |
| **D-Pad Up / Down / Left / Right** | `Up`, `Down`, `Left`, `Right` | Hides cursor immediately. Navigates app tiles on the Home Page, native TV UIs (`youtube.com/tv`), injected semantic/spatial navigators, and Settings Menu categories/submenus (`Right`/`Enter` enters submenu, `Left`/`Back` returns). |
| **OK / Select** | `Enter` | Launches the selected app tile on the Home Page, activates the focused button/tile/dropdown, opens drilldown or confirms selection in the Settings Menu, or focuses/submits search inputs. |
| **Back** | `BrowserBack`, `Escape`, `Backspace` | Closes open dropdowns/drawers/video players, navigates back from drilldown submenus in the Settings Menu, passes `Escape`/`Backspace` to native TV apps (`youtube.com/tv`), navigates back in history, or returns to the Launchpad if at the top-level page. |
| **Home / Menu** | `F1` or `Home` | Closes any active external application (e.g., `Plex HTPC`) and returns to the local 10-foot Home Theater Launchpad (`https://launchpad.example/index.html`). |
| **Settings Menu** | `F2` | Opens the 10-foot Settings popup menu in the top-left to configure Video presets (Standard, Cinema Home), Audio playback devices, Clarity motion presets (Off, Cinematic, Natural), and System actions (Force Home Theater). Pressing `F2` again closes the menu. |
| **Play / Pause** | `MediaPlayPause` | Toggles play/pause on the active HTML5 `<video>` element. |
| **Power / Exit** | `F12`, `Alt + F4`, `Ctrl + Q` | Cleanly closes any launched external app, exits the kiosk, and restores the system cursor. |
| **GPU Diagnostics** | `Ctrl + Shift + G` | Navigates to `edge://gpu` to verify PlayReady SL3000 & HEVC hardware support. |

---

## Configuration (`config.json`)

The application reads `config.json` from the application directory on startup (`TvKioskWrapper/config.json` is automatically copied to the build output directory):

```json
{
  "defaultUrl": "https://launchpad.example/index.html",
  "cursorHideDelaySeconds": 2,
  "debug": false,
  "colorControl": {
    "enabled": true,
    "executablePath": "W:\\Programs\\ColorControl\\ColorControl.exe"
  },
  "services": [
    {
      "name": "HBO Max",
      "subtitle": "4K UHD & Dolby Vision / HDR",
      "url": "https://play.hbomax.com",
      "accentColor": "#002BE7",
      "navigator": "hbomax",
      "spatialNavigation": true
    },
    {
      "name": "YouTube on TV",
      "subtitle": "Native 10-Foot 4K HDR UI",
      "url": "https://www.youtube.com/tv",
      "accentColor": "#FF0000",
      "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Cobalt/27.lts.3.1040976-gold (unlike Gecko) v8/13.8.258.54-jit gles Starboard/18, TvKioskWrapper_DESKTOP_x8664_2026/1.0.0 (TvKioskWrapper, Windows)",
      "navigator": "native",
      "spatialNavigation": false
    },
    {
      "name": "Plex HTPC",
      "subtitle": "Native 10-Foot Home Media Player",
      "executablePath": "C:\\Program Files\\Plex\\Plex HTPC\\Plex HTPC.exe",
      "accentColor": "#E5A00D",
      "spatialNavigation": false
    },
    {
      "name": "Prime Video",
      "subtitle": "Amazon Originals & Movies",
      "url": "https://www.primevideo.com",
      "accentColor": "#00A8E1",
      "navigator": "primevideo",
      "spatialNavigation": true
    }
  ]
}
```

### Configuration Fields
- **`defaultUrl`**: Initial URL loaded when the application starts (`https://launchpad.example/index.html` loads the local Launchpad Home Page).
- **`cursorHideDelaySeconds`**: Seconds of mouse inactivity before the cursor is hidden (default: `2`).
- **`debug`**: Enables WebView2 DevTools when set to `true` (can also be enabled via `--debug`).
- **`colorControl`**: LG TV display preset automation via ColorControl:
  - **`enabled`**: Enables or disables ColorControl preset switching (default: `true`).
  - **`executablePath`**: Path to `ColorControl.exe` (default: `W:\Programs\ColorControl\ColorControl.exe`).
- **`services`**: List of apps rendered on the Launchpad Home Page:
  - **`logo`** *(optional)*: Relative path to the app's logo image within the launcher assets (e.g., `"logos/hbomax.svg"`).
  - **`url`** *(optional)*: Target web application URL for WebView2 services.
  - **`executablePath`** *(optional)*: Path to a native desktop executable (such as `Plex HTPC.exe`) launched in front of the kiosk with global `F1` return-to-launchpad support.
  - **`navigator`** *(optional)*: D-Pad navigation strategy (`"hbomax"`, `"primevideo"`, `"spatial"`, or `"native"`).
  - **`userAgent`** *(optional)*: Custom `User-Agent` string applied when navigating to this service (omitted/null restores the default Windows Edge `User-Agent`).
  - **`spatialNavigation`**: Set to `true` for desktop websites that use an injected semantic or spatial navigator; set to `false` for native 10-foot UIs.

---

## Project Architecture

```text
w:\CSharpProjects\HomeTheater\
├── HomeTheater.slnx
├── AGENTS.md
├── README.md
├── TvKioskWrapper\
│   ├── Assets\Launcher\                # Local 10-foot Launchpad (index.html, launcher.css, launcher.js, logos/)
│   ├── Automation\                     # Localhost HTTP automation server (--automation) & JS inspectors
│   ├── Configuration\                  # AppConfig, ConfigLoader & StreamingServicePreset models
│   ├── Input\                          # InputController & BrowserShortcutFilter
│   ├── Interop\                        # Win32 user32.dll P/Invoke helpers (NativeMethods)
│   ├── Scripts\                        # ScriptFileLoader, ScriptInjector, bootstrap & per-app JS navigators
│   │   ├── Js\                         # document-bootstrap.js, spatial-navigation.js & on-screen-keyboard.js
│   │   └── Navigators\Js\              # semantic-navigator-core.js, hbomax-navigator.js, primevideo-navigator.js
│   ├── Shell\                          # SettingsMenuModalWindow, CursorAutoHider, ExternalAppManager, AudioDeviceManager, ColorControlManager & IKioskShellActions
│   ├── WebView\                        # WebViewManager, LauncherHostManager & UserAgentOverrideHandler
│   ├── MainWindow.xaml / .cs           # Fullscreen borderless WebView2 shell
│   └── config.json                     # Default kiosk settings and streaming service presets
└── TvKioskWrapper.Tests\
    ├── Infrastructure\                 # KioskAutomationFixture & compact JSON status models
    ├── Navigators\                     # HboMaxNavigatorTests, PrimeVideoNavigatorTests & OnScreenKeyboardTests
    └── Shell\                          # SettingsMenuTests (D-Pad navigation, submenus, video & audio selection)
```

---

## Verifying 4K PlayReady DRM & Hardware Decode

1. Launch the app and press `Ctrl + Shift + G` (or run with `--gpu`) to open `edge://gpu`.
2. Scroll to **Media Foundation Rendering Capabilities** and verify:
   - `PlayReady Hardware DRM disabled`: `false`
   - `HEVC installed`: `true`
   - `HDCP 2.2 support`: `true`


