# AGENTS.md

## Overview
**`TvKioskWrapper`** is a `.NET 10` (`net10.0-windows`) WPF + WebView2 fullscreen kiosk application for a Home Theater PC controlled via a remote control (FLIRC USB IR receiver emitting standard keyboard keys).

It wraps streaming web apps in a borderless 10-foot interface with hardware-accelerated 4K HDR playback (PlayReady DRM + HEVC), a local Launchpad home screen, and D-Pad (arrow key) navigation.

## Build & Run
```powershell
dotnet build
dotnet run --project TvKioskWrapper
```

## Architecture & Directory Layout
Code is organized into small, single-purpose files grouped by domain under `TvKioskWrapper/`:

- **`config.json`** — Single source of truth for application settings and the list of streaming service presets (`name`, `url`, `executablePath`, `subtitle`, `accentColor`, `navigator`, `spatialNavigation`, `userAgent`).
- **`MainWindow.xaml` / `MainWindow.xaml.cs`** — Fullscreen borderless window coordinating the shell, input controller, cursor auto-hider, external app manager, and WebView2 manager.
- **`Assets/Launcher/`** (`index.html`, `launcher.css`, `launcher.js`) — Local Launchpad homepage mapped to `https://launchpad.example/index.html`. Renders the app grid dynamically from `config.json`.
- **`Configuration/`** — Loads `config.json` and command-line overrides (`--url`, `--debug`, `--cursor-timeout`) into `AppConfig` and matches navigated URLs to `StreamingServicePreset` entries.
- **`WebView/`** — Initializes the WebView2 environment (PlayReady/HEVC browser flags, isolated profile in `%LocalAppData%\TvKioskWrapper\Profile`), maps the virtual host for the Launchpad, and applies per-service User-Agent overrides.
- **`Input/`** — Intercepts remote/keyboard input (`F1`/`Home` for Launchpad, `F12`/`Alt+F4`/`Ctrl+Q` to exit, `Escape`/`Backspace` for back navigation, `MediaPlayPause`, and D-Pad arrow keys) and blocks unwanted browser shortcuts.
- **`Scripts/` & `Scripts/Navigators/`** — Injects JavaScript into WebView2 for scrollbar hiding, DOM focus management, Smart TV capability spoofing, and tailored per-app semantic D-Pad navigators. Multi-line JS lives in actual `.js` files under `Js/` subfolders and is loaded via `ScriptFileLoader`.
- **`Automation/`** — Lightweight localhost HTTP server (`--automation`, default `http://127.0.0.1:9333/`) for background semantic DOM inspection (`/semantic-tree`), key simulation (`/key`), live JS evaluation (`/eval`), and downscaled low-res JPEG screenshots (`/screenshot?width=800&quality=55`).
- **`Shell/`** — Shell interfaces (`IKioskShellActions`), inactivity cursor auto-hiding (`CursorAutoHider`), and native desktop app launching/yielding with global `F1`/`F12` hotkey interception (`ExternalAppManager`).
- **`Interop/`** — Win32 P/Invoke helpers (`NativeMethods`).

## Key Conventions & Design Rules
1. **Small, Focused Files:** Keep classes small and focused on a single responsibility. Place new types in the appropriate domain folder rather than expanding existing files into monoliths, and keep injected JavaScript in standalone `.js` files loaded via `ScriptFileLoader`.
2. **Single Source of Truth (`config.json`):** Never hardcode streaming service URLs, executable paths, names, User-Agents, or per-site flags in C# or JavaScript. Add or modify services in `TvKioskWrapper/config.json` and read them via `AppConfig`.
3. **Navigation Modes (`navigator` / `executablePath` in `config.json`):**
   - `executablePath`: Launches a native 10-foot desktop application (e.g., `Plex HTPC.exe`) in front of the kiosk via `ExternalAppManager` while intercepting global `F1` to close the app and return to the Launchpad.
   - `"native"`: Web services with native 10-foot TV UIs (e.g., YouTube TV) handle arrow keys natively.
   - `"hbomax"` (and future per-app navigators under `Scripts/Navigators/Js/`): Tailored semantic D-Pad adapters mounted onto the shared `semantic-navigator-core.js` engine.
   - `"spatial"`: Generic 2D fallback navigator (`spatial-navigation.js`).
4. **Credit-Efficient UI Automation (`--automation`):**
   - Run `TvKioskWrapper.exe --automation` (which runs unobtrusively offscreen without stealing focus) and query `/semantic-tree` and `/key` (compact JSON) instead of dumping raw HTML or taking full-resolution screenshots. When screenshots are needed, use `/screenshot?width=800&quality=55`.
5. **Virtual Hostname:** Keep the local Launchpad on `launchpad.example` (avoid `.local` domains, which trigger slow mDNS lookups in Chromium on Windows).
