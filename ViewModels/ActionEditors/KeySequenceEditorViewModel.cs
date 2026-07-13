using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor fuer Tastensequenzen: Einzeltasten-Erfassung (Simple Mode) und
/// KeyDown/KeyUp-Aufnahme (Advanced Mode), inkl. Kernel-Injection-Option.
/// Hook-Events kommen vom Hook-Thread und werden auf den UI-Thread marshalled.
/// </summary>
public partial class KeySequenceEditorViewModel : ActionEditorViewModelBase
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly DispatcherQueue _dispatcherQueue;

    private bool _isCapturingKeyStroke;
    private bool _isRecording;

    public override string DisplayName => "Tastensequenz senden";
    public override ActionType Type => ActionType.KeySequence;
    public override string ValidationMessage => "Bitte füge mindestens eine Taste zur Sequenz hinzu.";

    public ObservableCollection<KeyStroke> KeyStrokes { get; } = new();

    [ObservableProperty]
    private bool _isAdvancedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKernelWarningVisible))]
    private bool _useKernelInjection;

    [ObservableProperty]
    private string _addKeyButtonText = "Taste erfassen";

    [ObservableProperty]
    private bool _isAddKeyEnabled = true;

    [ObservableProperty]
    private string _recordButtonText = "Aufnahme starten";

    [ObservableProperty]
    private string _recordButtonGlyph = "";

    [ObservableProperty]
    private bool _isRecordingActive;

    [ObservableProperty]
    private bool _isEmptyHintVisible = true;

    // Laeuft der InterceptionService bereits, den Status direkt ablesen — IsDriverActive()
    // (InputInterceptor.Initialize) wuerde bei laufendem Hook einen zweiten
    // Interception-Context erzeugen (native Access Violation).
    public bool IsKernelWarningVisible => UseKernelInjection
        && !(App.Current.InterceptionService.IsRunning || InterceptionService.IsDriverActive());

    public KeySequenceEditorViewModel(
        KeyboardHookService? keyboardHook = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        _keyboardHook = keyboardHook ?? App.Current.KeyboardHookService;
        _dispatcherQueue = dispatcherQueue ?? App.Current.DispatcherQueue;

        KeyStrokes.CollectionChanged += (s, e) => IsEmptyHintVisible = KeyStrokes.Count == 0;
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        if (!value && _isRecording)
            StopRecording();
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not KeySequenceAction keySequence) return;

        KeyStrokes.Clear();
        foreach (var stroke in keySequence.Keys)
            KeyStrokes.Add(stroke);

        // Advanced Mode erkennen: Sequenz enthaelt separate KeyDown/KeyUp-Eintraege
        IsAdvancedMode = keySequence.Keys.Any(k => k.EventType != KeyEventType.KeyPress);
        UseKernelInjection = keySequence.UseKernelInjection;
    }

    public override ActionBase? BuildAction()
    {
        if (KeyStrokes.Count == 0)
            return null;

        return new KeySequenceAction
        {
            Keys = KeyStrokes.ToList(),
            UseKernelInjection = UseKernelInjection
        };
    }

    public override void CancelCapture()
    {
        StopKeyStrokeCapture();
        StopRecording();
    }

    [RelayCommand]
    private void AddKeyStroke()
    {
        if (_isCapturingKeyStroke || _isRecording) return;

        _isCapturingKeyStroke = true;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressedForKeyStroke;

        AddKeyButtonText = "Drücke eine Taste...";
        IsAddKeyEnabled = false;
    }

    private void StopKeyStrokeCapture()
    {
        if (!_isCapturingKeyStroke) return;

        _isCapturingKeyStroke = false;
        _keyboardHook.KeyPressed -= OnKeyPressedForKeyStroke;
        _keyboardHook.IsCapturing = false;

        AddKeyButtonText = "Taste erfassen";
        IsAddKeyEnabled = true;
    }

    /// <summary>
    /// Fuegt eine manuell aus der Liste gewaehlte Taste hinzu (z.B. F13-F24, Medientasten,
    /// die per Erfassung nicht erreichbar sind). Im Advanced Mode als KeyDown+KeyUp-Paar.
    /// </summary>
    public void AddManualKey(KeyHelper.VirtualKeyEntry entry)
    {
        KeyStrokes.Add(new KeyStroke
        {
            VirtualKeyCode = entry.VirtualKeyCode,
            Modifiers = ModifierKeys.None,
            DelayAfterMs = 50,
            DisplayText = entry.Name,
            EventType = IsAdvancedMode ? KeyEventType.KeyDown : KeyEventType.KeyPress
        });

        if (IsAdvancedMode)
        {
            KeyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = entry.VirtualKeyCode,
                Modifiers = ModifierKeys.None,
                DelayAfterMs = 10,
                DisplayText = entry.Name,
                EventType = KeyEventType.KeyUp
            });
        }
    }

    private void OnKeyPressedForKeyStroke(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        _dispatcherQueue.TryEnqueue(() =>
        {
            KeyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = e.VirtualKeyCode,
                Modifiers = e.Modifiers,
                DelayAfterMs = 50,
                DisplayText = e.KeyDisplayText
            });
            StopKeyStrokeCapture();
        });
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_isRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (_isRecording || _isCapturingKeyStroke) return;

        _isRecording = true;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.CaptureModifierKeys = true; // Advanced Mode zeichnet auch Modifier-Tasten separat auf
        _keyboardHook.KeyPressed += OnRecordKeyDown;
        _keyboardHook.KeyReleased += OnRecordKeyUp;

        RecordButtonText = "Aufnahme stoppen";
        RecordButtonGlyph = "";
        IsRecordingActive = true;
    }

    private void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;
        _keyboardHook.KeyPressed -= OnRecordKeyDown;
        _keyboardHook.KeyReleased -= OnRecordKeyUp;
        _keyboardHook.IsCapturing = false;
        _keyboardHook.CaptureModifierKeys = false;

        RecordButtonText = "Aufnahme starten";
        RecordButtonGlyph = "";
        IsRecordingActive = false;
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        AddRecordedStroke(e.VirtualKeyCode, KeyEventType.KeyDown);
    }

    private void OnRecordKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        AddRecordedStroke(e.VirtualKeyCode, KeyEventType.KeyUp);
    }

    private void AddRecordedStroke(int virtualKeyCode, KeyEventType eventType)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            KeyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = virtualKeyCode,
                Modifiers = ModifierKeys.None, // Modifiers werden im Advanced Mode als eigene Events aufgezeichnet
                DelayAfterMs = 10,
                DisplayText = KeyHelper.GetKeyName(virtualKeyCode),
                EventType = eventType
            });
        });
    }

    [RelayCommand]
    private void RemoveKeyStroke(KeyStroke? stroke)
    {
        if (stroke != null)
            KeyStrokes.Remove(stroke);
    }
}
