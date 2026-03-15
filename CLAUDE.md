# HotKeyManager — CLAUDE.md

## Project Overview

Windows-native application for managing global hotkeys system-wide. Users define hotkeys (key + modifiers) that trigger various actions when pressed anywhere in Windows.

**UI language:** German

## Tech Stack

- **Language:** C# 12 / .NET 9.0
- **Framework:** WinUI 3 (Windows App SDK 1.6)
- **Target:** Windows 10 1803+ (net9.0-windows10.0.22621.0)
- **Key NuGet packages:**
  - `H.NotifyIcon.WinUI` — system tray
  - `CommunityToolkit.Mvvm` — MVVM helpers
  - `InputInterceptor` — kernel-mode keyboard driver
  - `System.Text.Json` — config serialization

## Project Structure

```
/
├── App.xaml / App.xaml.cs         # Entry point, service initialization
├── MainWindow.xaml / .cs          # Main window shell
├── HotKeyManager.csproj           # MSBuild / NuGet config
├── build.ps1                      # Build & installer script
├── app.manifest                   # Windows UAC / compatibility
├── Services/                      # All core business logic
├── Models/                        # Data models & action types
├── Views/                         # WinUI 3 pages & dialogs
├── Helpers/                       # KeyHelper, WindowHelper, GlobalConst
└── installer/                     # Inno Setup installer config
```

## Key Architecture Decisions

### Service Layer (Services/)
| Service | Responsibility |
|---|---|
| `HotkeyManagerService` | Orchestrates hotkey detection → action execution |
| `KeyboardHookService` | Win32 low-level keyboard hook (user-mode) |
| `InterceptionService` | Kernel-mode hook via InputInterceptor driver |
| `ActionExecutor` | Runs all action types |
| `ConfigurationService` | Load/save `%APPDATA%/HotKeyManager/config.json` |
| `AutoStartService` | Windows startup registry integration |
| `TrayIconService` | System tray icon & context menu |

### Action Types (Models/ — polymorphic via `$type` in JSON)
- `WebhookAction` — HTTP request (GET/POST/PUT/DELETE/PATCH)
- `KeySequenceAction` — Simulate key presses
- `SendTextAction` — Type text as Unicode keyboard input
- `ProcessAction` — Launch external process
- `BatchAction` — Execute CMD.exe batch command

### Hotkey Targeting
- `WindowTargetMode`: `None`, `OnlyWhenActive`, `SendToBackground`
- Wildcard patterns on window title (`*pattern*`)
- Win32 PostMessage for background window keystroke delivery

## Configuration

**File:** `%APPDATA%/HotKeyManager/config.json`

```json
{
  "hotkeys": [{
    "id": "guid",
    "name": "...",
    "virtualKeyCode": 123,
    "modifiers": "ctrl+alt",
    "isEnabled": true,
    "useKernelInterception": false,
    "action": { "$type": "...", ... },
    "windowMode": "None",
    "targetProcessName": "app.exe",
    "targetWindowTitle": "*title*"
  }],
  "settings": {
    "runAtStartup": false,
    "minimizeToTray": true,
    "startMinimized": false,
    "theme": "dark"
  }
}
```

## Build

```powershell
# Standard Release build
./build.ps1

# With installer (requires Inno Setup 6)
./build.ps1 -CreateInstaller

# Debug build
./build.ps1 -Configuration Debug

# Clean first
./build.ps1 -Clean
```

Output: `bin/publish/win-x64/HotKeyManager.exe` (self-contained, includes .NET runtime)

## Special Behaviors

- **Unsafe code** is enabled (`AllowUnsafeBlocks=true`) — used for Win32 P/Invoke
- **Kernel-mode driver** (`--install-driver` / `--uninstall-driver` CLI args) requires admin — app can re-launch itself elevated
- **No test suite** — manual testing only; `TestWinUI/` directory exists but is unused

## Views

| View | Purpose |
|---|---|
| `HotkeyListPage` | Main list of all configured hotkeys |
| `HotkeyEditorDialog` | Create / edit a hotkey |
| `KeyCaptureDialog` | Capture a key press from the user |
| `SettingsPage` | App settings (startup, tray, theme) |
