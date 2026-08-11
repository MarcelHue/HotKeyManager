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
- **Adding a new action type** = Model class (+ `JsonDerivedType` on `ActionBase`) + editor VM (extends `ActionEditorViewModelBase`) + editor UserControl + one DataTemplate entry in `ActionEditorTemplateSelector`/`HotkeyEditorPage.xaml` + one entry in `MacroEditorViewModel.CreateEditorFor` and the add-block flyout.
- `ActionEditorViewModelBase.BuildAction()` must **always** return an object, never null — macros are saveable as drafts with empty/incomplete blocks. Only name and at least one trigger are enforced.
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
| `MidiService` | MIDI in/out via **WinMM** (not WinRT — the MIDI 2.0 stack hides devices from `Windows.Devices.Midi`); 5 s rescan for hot-plug; NoteOn/CC input triggers hotkeys via optional `HotkeyDefinition.MidiTrigger` (keyboard and MIDI can coexist), `SendAsync` drives `MidiSendAction`. Always enumerate before opening a port in the same process, else BADDEVICEID |
| `AudioService` | Microphone mute (set/toggle) for the default capture device via Core Audio COM |
| `MidiPresetService` | Loads documented MIDI command presets (JSON) from `Presets\midi` next to the exe and from `%APPDATA%\HotKeyManager\Presets\midi`; user presets override built-ins by name. Import copies a file into the user folder after validating it. `Save()` always writes to the user folder, so edits survive app updates and the bundled preset stays untouched — the editor uses this to add/update entries and mark them verified |

### Action Types (Models/ — polymorphic via `$type` in JSON)
- `WebhookAction` — HTTP request (GET/POST/PUT/DELETE/PATCH)
- `KeySequenceAction` — Simulate key presses
- `SendTextAction` — Type text as Unicode keyboard input (`CharDelayMs` = typing speed)
- `ProcessAction` — Launch external process
- `BatchAction` — Execute CMD.exe batch command
- `DelayAction` — Wait N ms (mainly as macro step)
- `SystemAction` — Windows media keys (play/pause, next/prev, volume) + microphone mute via Core Audio (`AudioService`)
- `MidiSendAction` — Send NoteOn/CC to a MIDI output device (`MidiService.SendAsync`), e.g. back to the Rodecaster. `ToggleValues` alternates between `Value`/`ValueB` on each run and persists `LastSentWasB`, for devices with separate on/off commands whose state cannot be read back (Rodecaster: CC 27 value 1 = muted, 0 = unmuted)
- `MacroAction` — Composite: ordered `Steps` list of any of the above (no macro nesting), executed top-down with optional global `StepDelayMs` between steps. Editor reuses the per-type editor UserControls as reorderable expander blocks (`MacroEditorView`).

**Since v1.3.0 `MacroAction` is the ONLY top-level action type** — the editor offers no type dropdown anymore; all other types exist solely as macro steps. `LegacyActionMigration.WrapLegacyActions` wraps old single actions into one-step macros on app start and on config import; the editor also wraps transparently when loading an unmigrated hotkey.

**Macro editor has two modes** (`MacroEditorViewModel`): *Einfach* (default) exposes fixed fields — key before / text / key after (`KeyCaptureField` is the reusable capture control); *Erweitert* is the full reorderable block list. `TryLoadSimple` decides the mode when loading: a macro qualifies as simple if it has ≤3 steps, exactly one `SendTextAction`, and the surrounding steps are single-key `KeySequenceAction`s without kernel injection. Switching modes migrates the content both ways.

**Duplicating a hotkey**: list passes `EditorNavArgs { Hotkey = deepCopy, IsDuplicate = true }`; the editor loads everything but clears both triggers and saves as a new hotkey. Deep copies go through `Helpers/ActionCloner` (JSON roundtrip, keeps `$type` polymorphism).

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
| `HotkeyListPage` | Hotkeys grouped into collapsible folders (`HotkeyDefinition.Group`, one level). Per row: drag handle (drag to reorder / drop onto another folder, **click** for a move menu), trigger badge with a flyout to (re)capture key/MIDI inline, inline rename (double-tap or pencil), toggle, run, folder assign, edit, duplicate, delete. Search disables reordering. Folder header has a menu to rename (also via double-tap on the name) or dissolve the folder. `DragItemsStarting` clears the auto-generated text in the data package and sets an app-private format — otherwise dropping outside the window pastes/searches that text in other apps (e.g. Firefox) |
| `HotkeyEditorPage` | Create / edit a hotkey (full page, scrolls; key capture + window capture) |
| `Views/ActionEditors/*` | One UserControl per action type, selected via `ActionEditorTemplateSelector` |
| `SettingsPage` | Theme, startup/tray, kernel driver, import/export, update check |
