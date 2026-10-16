using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Reusable field that captures a single key combination (hotkey trigger, key before/after in the
/// simple macro form). Hook events arrive on the hook thread and are marshalled to the UI thread.
/// </summary>
public partial class KeyCaptureField : ObservableObject
{
    private const string NotSetText = "Nicht gesetzt";
    private const string DefaultButtonText = "Erfassen";
    private const int KeyPressDelayMs = 50;

    private readonly KeyboardHookService keyboardHook;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly string idleButtonText;
    private bool isCapturing;

    [ObservableProperty]
    private string displayText = NotSetText;

    [ObservableProperty]
    private string buttonText;

    [ObservableProperty]
    private bool isButtonEnabled = true;

    [ObservableProperty]
    private bool hasKey;

    /// <summary>Creates the field; services that are not passed in default to the app-wide instances.</summary>
    /// <param name="idleButtonText">Caption of the capture button while no capture is running.</param>
    public KeyCaptureField(
        string idleButtonText = DefaultButtonText,
        KeyboardHookService? keyboardHook = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        this.idleButtonText = idleButtonText;
        this.buttonText = idleButtonText;
        this.keyboardHook = keyboardHook ?? App.Current.KeyboardHookService;
        this.dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;
    }

    /// <summary>Virtual key code of the captured key, 0 if none is set.</summary>
    public int VirtualKeyCode { get; private set; }

    /// <summary>Modifier keys of the captured combination.</summary>
    public ModifierKeys Modifiers { get; private set; }

    /// <summary>True when a key is set.</summary>
    public bool IsSet => this.VirtualKeyCode != 0;

    /// <summary>Sets the key combination without capturing it.</summary>
    public void Set(int virtualKeyCode, ModifierKeys modifiers)
    {
        this.VirtualKeyCode = virtualKeyCode;
        this.Modifiers = modifiers;
        this.UpdateDisplay();
    }

    /// <summary>Stops a running capture and keeps the previous key.</summary>
    public void StopCapture()
    {
        if (!this.isCapturing)
        {
            return;
        }

        this.isCapturing = false;
        this.keyboardHook.KeyPressed -= this.OnKeyPressed;
        this.keyboardHook.IsCapturing = false;

        this.ButtonText = this.idleButtonText;
        this.IsButtonEnabled = true;
        this.UpdateDisplay();
    }

    /// <summary>Builds the matching key stroke, or null if no key is set.</summary>
    public KeyStroke? ToKeyStroke() => this.VirtualKeyCode == 0
        ? null
        : new KeyStroke
        {
            VirtualKeyCode = this.VirtualKeyCode,
            Modifiers = this.Modifiers,
            DelayAfterMs = KeyPressDelayMs,
            DisplayText = this.DisplayText,
        };

    [RelayCommand]
    private void StartCapture()
    {
        if (this.isCapturing)
        {
            return;
        }

        this.isCapturing = true;
        this.keyboardHook.IsCapturing = true;
        this.keyboardHook.KeyPressed += this.OnKeyPressed;

        this.DisplayText = "Drücke eine Taste...";
        this.ButtonText = "Erfassen...";
        this.IsButtonEnabled = false;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        this.dispatcherQueue.TryEnqueue(() =>
        {
            this.VirtualKeyCode = e.VirtualKeyCode;
            this.Modifiers = e.Modifiers;
            this.StopCapture();
        });
    }

    [RelayCommand]
    private void Clear()
    {
        this.StopCapture();
        this.Set(0, ModifierKeys.None);
    }

    private void UpdateDisplay()
    {
        this.HasKey = this.VirtualKeyCode != 0;
        this.DisplayText = this.HasKey ? KeyHelper.FormatCombination(this.VirtualKeyCode, this.Modifiers) : NotSetText;
    }
}
