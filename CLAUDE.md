# HotKeyManager — CLAUDE.md

## Project Overview

Windows-native application for managing global hotkeys system-wide. Users define hotkeys (key + modifiers) that trigger various actions when pressed anywhere in Windows.

**UI language:** German

## Tech Stack

- **Language:** C# 12 / .NET 9.0
- **Framework:** WinUI 3 (Windows App SDK 1.6), MVVM via CommunityToolkit.Mvvm
- **Target:** Windows 10 1803+ (net9.0-windows10.0.22621.0)
- **Key NuGet packages:**
  - `H.NotifyIcon.WinUI` — system tray
  - `CommunityToolkit.Mvvm` — MVVM (ObservableObject, [ObservableProperty], [RelayCommand])
  - `InputInterceptor` — kernel-mode keyboard driver
  - `Velopack` — auto-update via GitHub Releases
  - `System.Text.Json` — config serialization

## Project Structure

```
/
├── Program.cs                     # Custom Main: VelopackApp.Build().Run() before XAML start
├── App.xaml / App.xaml.cs         # Application, service singletons (App.Current.<Service>)
├── MainWindow.xaml / .cs          # Shell: custom titlebar, Mica, NavigationView, update InfoBar
├── HotKeyManager.csproj           # Version property (overridden by release workflow)
├── build.ps1                      # Local build (dotnet CLI; -Pack for local Velopack test)
├── .github/workflows/             # build.yml (CI check), release.yml (tag → GitHub Release)
├── Services/                      # Core logic incl. ThemeService, UpdateService
├── Models/                        # Data models & action types
├── ViewModels/                    # Page ViewModels + ActionEditors/ (one VM per action type)
├── Views/                         # Pages + ActionEditors/ (one UserControl per action type)
└── Helpers/                       # KeyHelper, WindowHelper, GlobalConst
```

## Key Architecture Decisions

### MVVM & UI

- Pages are thin; logic lives in `ViewModels/` (CommunityToolkit.Mvvm, `x:Bind`).
- ViewModels get services via constructor defaults from `App.Current` singletons (no DI container).
- Hotkey editor is a **page** (`HotkeyEditorPage`), navigated with `EditorNavArgs { Hotkey?, PreselectType? }` — not a dialog.
- **Adding a new action type** = Model class (+ `JsonDerivedType` on `ActionBase`) + editor VM (extends `ActionEditorViewModelBase`) + editor UserControl + one DataTemplate entry in `ActionEditorTemplateSelector`/`HotkeyEditorPage.xaml` + one entry in `HotkeyEditorViewModel.ActionEditors`.
- Theme (light/dark/system) via `ThemeService.Apply()` — sets `RequestedTheme` on window content, persisted in `AppSettings.Theme`.
- Keyboard-hook events arrive on the **hook thread**; ViewModels marshal via `DispatcherQueue.TryEnqueue`.
- Editor page calls `ViewModel.CancelAllCaptures()` in `OnNavigatedFrom` — never leave `KeyboardHookService.IsCapturing` set.

### Service Layer (Services/)
| Service | Responsibility |
|---|---|
| `HotkeyManagerService` | Orchestrates hotkey detection → action execution; `HotkeysChanged` event; `SaveChanges()` persists without event |
| `KeyboardHookService` | Win32 low-level keyboard hook (user-mode) |
| `InterceptionService` | Kernel-mode hook via InputInterceptor driver |
| `ActionExecutor` | Runs all action types |
| `ConfigurationService` | Load/save `%APPDATA%/HotKeyManager/config.json` |
| `AutoStartService` | Windows startup registry integration (path refreshed on every app start) |
| `TrayIconService` | System tray icon & context menu |
| `ThemeService` | Apply light/dark/system theme + titlebar button colors |
| `UpdateService` | Velopack update check/download against GitHub Releases (no-op in dev builds) |

### Action Types (Models/ — polymorphic via `$type` in JSON)
- `WebhookAction` — HTTP request (GET/POST/PUT/DELETE/PATCH)
- `KeySequenceAction` — Simulate key presses
- `SendTextAction` — Type text as Unicode keyboard input (`CharDelayMs` = typing speed)
- `ProcessAction` — Launch external process
- `BatchAction` — Execute CMD.exe batch command
- `DelayAction` — Wait N ms (mainly as macro step)
- `MacroAction` — Composite: ordered `Steps` list of any of the above (no macro nesting), executed top-down with optional global `StepDelayMs` between steps. Editor reuses the per-type editor UserControls as reorderable expander blocks (`MacroEditorView`).

**Since v1.3.0 `MacroAction` is the ONLY top-level action type** — the editor offers no type dropdown anymore; all other types exist solely as macro steps. `LegacyActionMigration.WrapLegacyActions` wraps old single actions into one-step macros on app start and on config import; the editor also wraps transparently when loading an unmigrated hotkey.

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

## Build & Release

Local builds use the dotnet CLI (no Visual Studio/MSBuild required):

```powershell
# Release publish to bin/publish/win-x64
./build.ps1

# Debug build
dotnet build HotKeyManager.csproj -c Debug -p:Platform=x64

# Local Velopack package (needs: dotnet tool install -g vpk)
./build.ps1 -Pack
```

**Official releases via GitHub Actions:** push a tag `vX.Y.Z` → `.github/workflows/release.yml` publishes, packs with Velopack (`vpk pack` + `vpk upload github`) and creates the GitHub Release with Setup exe, portable zip and delta packages. The csproj `<Version>` is overridden with the tag version.

**Auto-update:** the installed app checks GitHub Releases on startup and every 15 min (`UpdateService`; persistent update button in the NavigationView pane footer); manual check in Settings. Update checks are a no-op when running from a build directory (`UpdateManager.IsInstalled == false`).

## Special Behaviors

- **Unsafe code** is enabled (`AllowUnsafeBlocks=true`) — used for Win32 P/Invoke
- **Kernel-mode driver** (`--install-driver` / `--uninstall-driver` CLI args) requires admin — app can re-launch itself elevated
- **Custom Main** in `Program.cs` (`DISABLE_XAML_GENERATED_MAIN`) — Velopack hooks must run before XAML starts
- **Do not enable `PublishSingleFile`** — incompatible with Velopack packaging
- **No test suite** — manual testing only; `TestWinUI/` directory exists but is unused

## Views

| View | Purpose |
|---|---|
| `HotkeyListPage` | List of hotkeys: search, toggle, run, edit, delete; "Neues Text-Makro" quick entry |
| `HotkeyEditorPage` | Create / edit a hotkey (full page, scrolls; key capture + window capture) |
| `Views/ActionEditors/*` | One UserControl per action type, selected via `ActionEditorTemplateSelector` |
| `SettingsPage` | Theme, startup/tray, kernel driver, import/export, update check |
