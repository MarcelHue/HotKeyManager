using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Wiederverwendbares Feld zum Erfassen einer einzelnen Tastenkombination
/// (Einfach-Modus des Makro-Editors: Taste davor / Taste danach).
/// Hook-Events kommen vom Hook-Thread und werden auf den UI-Thread marshalled.
/// </summary>
public partial class KeyCaptureField : ObservableObject
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isCapturing;

    public int VirtualKeyCode { get; private set; }
    public ModifierKeys Modifiers { get; private set; }

    [ObservableProperty]
    private string _displayText = "Nicht gesetzt";

    [ObservableProperty]
    private string _buttonText = "Erfassen";

    [ObservableProperty]
    private bool _isButtonEnabled = true;

    [ObservableProperty]
    private bool _hasKey;

    public bool IsSet => VirtualKeyCode != 0;

    public KeyCaptureField(KeyboardHookService? keyboardHook = null, DispatcherQueue? dispatcherQueue = null)
    {
        _keyboardHook = keyboardHook ?? App.Current.KeyboardHookService;
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;
    }

    public void Set(int virtualKeyCode, ModifierKeys modifiers)
    {
        VirtualKeyCode = virtualKeyCode;
        Modifiers = modifiers;
        UpdateDisplay();
    }

    [RelayCommand]
    private void StartCapture()
    {
        if (_isCapturing) return;

        _isCapturing = true;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressed;

        DisplayText = "Drücke eine Taste...";
        ButtonText = "Erfassen...";
        IsButtonEnabled = false;
    }

    public void StopCapture()
    {
        if (!_isCapturing) return;

        _isCapturing = false;
        _keyboardHook.KeyPressed -= OnKeyPressed;
        _keyboardHook.IsCapturing = false;

        ButtonText = "Erfassen";
        IsButtonEnabled = true;
        UpdateDisplay();
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        _dispatcherQueue.TryEnqueue(() =>
        {
            VirtualKeyCode = e.VirtualKeyCode;
            Modifiers = e.Modifiers;
            StopCapture();
        });
    }

    [RelayCommand]
    private void Clear()
    {
        StopCapture();
        VirtualKeyCode = 0;
        Modifiers = ModifierKeys.None;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        HasKey = VirtualKeyCode != 0;

        if (VirtualKeyCode == 0)
        {
            DisplayText = "Nicht gesetzt";
            return;
        }

        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyHelper.GetKeyName(VirtualKeyCode));

        DisplayText = string.Join(" + ", parts);
    }

    /// <summary>Baut den passenden KeyStroke, oder null wenn keine Taste gesetzt ist.</summary>
    public KeyStroke? ToKeyStroke() => VirtualKeyCode == 0
        ? null
        : new KeyStroke
        {
            VirtualKeyCode = VirtualKeyCode,
            Modifiers = Modifiers,
            DelayAfterMs = 50,
            DisplayText = DisplayText
        };
}
