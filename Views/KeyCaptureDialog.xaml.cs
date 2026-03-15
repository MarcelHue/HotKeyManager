using Microsoft.UI.Xaml.Controls;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;

namespace HotKeyManager.Views;

public sealed partial class KeyCaptureDialog : ContentDialog
{
    public int CapturedKeyCode { get; private set; }
    public ModifierKeys CapturedModifiers { get; private set; }
    public string DisplayText => GetDisplayText();
    
    private KeyboardHookService? _keyboardHook;
    
    public KeyCaptureDialog()
    {
        this.InitializeComponent();
    }
    
    private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        _keyboardHook = App.Current.KeyboardHookService;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressed;
    }
    
    private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (_keyboardHook != null)
        {
            _keyboardHook.KeyPressed -= OnKeyPressed;
            _keyboardHook.IsCapturing = false;
        }
    }
    
    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        // Block the key from propagating
        e.Handled = true;
        
        // Update on UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            CapturedKeyCode = e.VirtualKeyCode;
            CapturedModifiers = e.Modifiers;
            
            KeyDisplayText.Text = GetDisplayText();
            IsPrimaryButtonEnabled = true;
        });
    }
    
    private string GetDisplayText()
    {
        if (CapturedKeyCode == 0)
            return "Warte auf Eingabe...";
            
        var parts = new List<string>();
        
        if (CapturedModifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (CapturedModifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (CapturedModifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (CapturedModifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");
        
        parts.Add(KeyHelper.GetKeyName(CapturedKeyCode));
        
        return string.Join(" + ", parts);
    }
}
