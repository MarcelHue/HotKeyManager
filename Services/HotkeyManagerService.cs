using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class HotkeyManagerService
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly ActionExecutor _actionExecutor;
    private readonly ConfigurationService _configService;
    private readonly List<HotkeyDefinition> _hotkeys = new();

    public IReadOnlyList<HotkeyDefinition> Hotkeys => _hotkeys.AsReadOnly();

    public event EventHandler? HotkeysChanged;

    public HotkeyManagerService(
        KeyboardHookService keyboardHook,
        ActionExecutor actionExecutor,
        ConfigurationService configService)
    {
        _keyboardHook = keyboardHook;
        _actionExecutor = actionExecutor;
        _configService = configService;

        _keyboardHook.KeyPressed += OnKeyPressed;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        if (_keyboardHook.IsCapturing) return;

        var matchingHotkey = _hotkeys.FirstOrDefault(h =>
            h.IsEnabled &&
            h.VirtualKeyCode == e.VirtualKeyCode &&
            h.Modifiers == e.Modifiers);

        if (matchingHotkey?.Action != null)
        {
            if (!ShouldExecuteHotkey(matchingHotkey))
                return;

            e.Handled = true;
            _ = ExecuteHotkeyAsync(matchingHotkey);
        }
    }

    private bool ShouldExecuteHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.WindowMode == WindowTargetMode.OnlyWhenActive)
            return WindowHelper.IsActiveWindowMatch(hotkey.TargetProcessName, hotkey.TargetWindowTitle);
        return true;
    }

    private async Task ExecuteHotkeyAsync(HotkeyDefinition hotkey)
    {
        try
        {
            await _actionExecutor.ExecuteAsync(
                hotkey.Action!,
                hotkey.WindowMode,
                hotkey.TargetProcessName,
                hotkey.TargetWindowTitle);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error executing hotkey action: {ex.Message}");
        }
    }

    public void LoadHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        _hotkeys.Clear();
        _hotkeys.AddRange(hotkeys);
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddHotkey(HotkeyDefinition hotkey)
    {
        _hotkeys.Add(hotkey);
        SaveAndNotify();
    }

    public void UpdateHotkey(HotkeyDefinition hotkey)
    {
        var index = _hotkeys.FindIndex(h => h.Id == hotkey.Id);
        if (index >= 0)
        {
            _hotkeys[index] = hotkey;
            SaveAndNotify();
        }
    }

    public void RemoveHotkey(Guid id)
    {
        var hotkey = _hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey != null)
        {
            _hotkeys.Remove(hotkey);
            SaveAndNotify();
        }
    }

    public void ToggleHotkey(Guid id)
    {
        var hotkey = _hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey != null)
        {
            hotkey.IsEnabled = !hotkey.IsEnabled;
            _configService.Configuration.Hotkeys = _hotkeys.ToList();
            _ = _configService.SaveAsync();
            // Kein HotkeysChanged - UI aktualisiert sich via INotifyPropertyChanged-Binding
        }
    }

    private void SaveAndNotify()
    {
        _configService.Configuration.Hotkeys = _hotkeys.ToList();
        _ = _configService.SaveAsync();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }
}
