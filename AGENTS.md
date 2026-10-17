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

- **`config.json`** — Single source of truth for application settings and the list of streaming service presets (`name`, `url`, `subtitle`, `accentColor`, `spatialNavigation`, `userAgent`).
- **`MainWindow.xaml` / `MainWindow.xaml.cs`** — Fullscreen borderless window coordinating the shell, input controller, cursor auto-hider, and WebView2 manager.
- **`Assets/Launcher/`** (`index.html`, `launcher.css`, `launcher.js`) — Local Launchpad homepage mapped to `https://launchpad.example/index.html`. Renders the app grid dynamically from `config.json`.
- **`Configuration/`** — Loads `config.json` and command-line overrides (`--url`, `--debug`, `--cursor-timeout`) into `AppConfig` and matches navigated URLs to `StreamingServicePreset` entries.
- **`WebView/`** — Initializes the WebView2 environment (PlayReady/HEVC browser flags, isolated profile in `%LocalAppData%\TvKioskWrapper\Profile`), maps the virtual host for the Launchpad, and applies per-service User-Agent overrides.
- **`Input/`** — Intercepts remote/keyboard input (`F1`/`Home` for Launchpad, `F12`/`Alt+F4`/`Ctrl+Q` to exit, `Escape`/`Backspace` for back navigation, `MediaPlayPause`, and D-Pad arrow keys) and blocks unwanted browser shortcuts.
- **`Scripts/`** — Injects JavaScript into WebView2 for scrollbar hiding, DOM focus management, Smart TV capability spoofing, and 2D spatial D-Pad navigation (`SpatialNavigationScript.cs`) for desktop websites that lack native TV navigation.
- **`Shell/`** — Shell interfaces (`IKioskShellActions`) and inactivity cursor auto-hiding (`CursorAutoHider`).
- **`Interop/`** — Win32 P/Invoke helpers (`NativeMethods`).

## Key Conventions & Design Rules
1. **Small, Focused Files:** Keep classes small and focused on a single responsibility. Place new types in the appropriate domain folder rather than expanding existing files into monoliths.
2. **Single Source of Truth (`config.json`):** Never hardcode streaming service URLs, names, User-Agents, or per-site flags in C# or JavaScript. Add or modify services in `TvKioskWrapper/config.json` and read them via `AppConfig`.
3. **Navigation Modes:**
   - Services with native 10-foot TV UIs (e.g., YouTube TV, Plex TV) set `"spatialNavigation": false` in `config.json` and handle arrow keys natively.
   - Standard desktop websites (e.g., Netflix, Prime Video) set `"spatialNavigation": true` in `config.json` to enable the injected 2D spatial navigation focus ring.
4. **Virtual Hostname:** Keep the local Launchpad on `launchpad.example` (avoid `.local` domains, which trigger slow mDNS lookups in Chromium on Windows).
