using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class HotkeyManagerService
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly InterceptionService _interceptionService;
    private readonly ActionExecutor _actionExecutor;
    private readonly ConfigurationService _configService;
    private readonly List<HotkeyDefinition> _hotkeys = new();
    
    public IReadOnlyList<HotkeyDefinition> Hotkeys => _hotkeys.AsReadOnly();
    
    public event EventHandler? HotkeysChanged;
    
    public HotkeyManagerService(
        KeyboardHookService keyboardHook, 
        InterceptionService interceptionService,
        ActionExecutor actionExecutor,
        ConfigurationService configService)
    {
        _keyboardHook = keyboardHook;
        _interceptionService = interceptionService;
        _actionExecutor = actionExecutor;
        _configService = configService;
        
        _keyboardHook.KeyPressed += OnKeyPressed;
        _interceptionService.HotkeyTriggered += OnInterceptionHotkeyTriggered;
    }
    
    /// <summary>
    /// Handler für Hotkeys die über den Interception-Treiber (Kernel-Mode) ausgelöst wurden.
    /// </summary>
    private void OnInterceptionHotkeyTriggered(object? sender, InterceptionKeyEventArgs e)
    {
        if (e.Hotkey.Action != null)
        {
            // Fenster-Check fuer OnlyWhenActive Modus
            if (!ShouldExecuteHotkey(e.Hotkey))
                return;
            
            _ = ExecuteHotkeyAsync(e.Hotkey);
        }
    }
    
    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        // Don't trigger hotkeys when capturing
        if (_keyboardHook.IsCapturing) return;
        
        var matchingHotkey = _hotkeys.FirstOrDefault(h => 
            h.IsEnabled && 
            !h.UseKernelInterception && // Kernel-Mode Hotkeys werden vom InterceptionService behandelt
            h.VirtualKeyCode == e.VirtualKeyCode && 
            h.Modifiers == e.Modifiers);
        
        if (matchingHotkey?.Action != null)
        {
            // Fenster-Check fuer OnlyWhenActive Modus
            if (!ShouldExecuteHotkey(matchingHotkey))
                return;
            
            e.Handled = true;
            _ = ExecuteHotkeyAsync(matchingHotkey);
        }
    }
    
    /// <summary>
    /// Prueft ob ein Hotkey ausgefuehrt werden soll basierend auf WindowMode.
    /// </summary>
    private bool ShouldExecuteHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.WindowMode == WindowTargetMode.OnlyWhenActive)
        {
            // Nur ausfuehren wenn das aktive Fenster passt
            return WindowHelper.IsActiveWindowMatch(hotkey.TargetProcessName, hotkey.TargetWindowTitle);
        }
        
        // Fuer None und SendToBackground: immer ausfuehren
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
        
        // Kernel-Mode Hotkeys beim InterceptionService registrieren
        _interceptionService.UpdateRegisteredHotkeys(_hotkeys);
        
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
            SaveAndNotify();
        }
    }
    
    private void SaveAndNotify()
    {
        _configService.Configuration.Hotkeys = _hotkeys.ToList();
        _ = _configService.SaveAsync();
        
        // Kernel-Mode Hotkeys beim InterceptionService aktualisieren
        _interceptionService.UpdateRegisteredHotkeys(_hotkeys);
        
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }
}
