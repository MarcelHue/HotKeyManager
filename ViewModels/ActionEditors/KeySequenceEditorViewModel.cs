using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor for key sequences: single key capture (simple mode) and KeyDown/KeyUp recording
/// (advanced mode), including the kernel injection option.
/// Hook events arrive on the hook thread and are marshalled to the UI thread.
/// </summary>
public partial class KeySequenceEditorViewModel : ActionEditorViewModelBase
{
    private const string CaptureKeyText = "Taste erfassen";
    private const string StartRecordingText = "Aufnahme starten";
    private const string RecordGlyph = "\uE7C8";
    private const string StopGlyph = "\uE71A";

    private const int KeyPressDelayMs = 50;
    private const int KeyEventDelayMs = 10;

    private readonly KeyboardHookService keyboardHook;
    private readonly DispatcherQueue dispatcherQueue;

    private bool isCapturingKeyStroke;
    private bool isRecording;

    [ObservableProperty]
    private bool isAdvancedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKernelWarningVisible))]
    private bool useKernelInjection;

    [ObservableProperty]
    private string addKeyButtonText = CaptureKeyText;

    [ObservableProperty]
    private bool isAddKeyEnabled = true;

    [ObservableProperty]
    private string recordButtonText = StartRecordingText;

    [ObservableProperty]
    private string recordButtonGlyph = RecordGlyph;

    [ObservableProperty]
    private bool isRecordingActive;

    [ObservableProperty]
    private bool isEmptyHintVisible = true;

    /// <summary>Creates the editor; services that are not passed in default to the app-wide instances.</summary>
    public KeySequenceEditorViewModel(
        KeyboardHookService? keyboardHook = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        this.keyboardHook = keyboardHook ?? App.Current.KeyboardHookService;
        this.dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;

        this.KeyStrokes.CollectionChanged += (s, e) => this.IsEmptyHintVisible = this.KeyStrokes.Count == 0;
    }

    /// <inheritdoc />
    public override string DisplayName => "Tastensequenz senden";

    /// <inheritdoc />
    public override ActionType Type => ActionType.KeySequence;

    /// <summary>The keys of the sequence in playback order.</summary>
    public ObservableCollection<KeyStroke> KeyStrokes { get; } = new();

    /// <summary>True when kernel injection is selected but the Interception driver is not active.</summary>
    /// <remarks>
    /// While the InterceptionService runs, its state is read directly: IsDriverActive() calls
    /// InputInterceptor.Initialize, which would create a second Interception context
    /// and crash with a native access violation.
    /// </remarks>
    public bool IsKernelWarningVisible => this.UseKernelInjection
        && !(App.Current.InterceptionService.IsRunning || InterceptionService.IsDriverActive());

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not KeySequenceAction keySequence)
        {
            return;
        }

        this.KeyStrokes.Clear();
        foreach (var stroke in keySequence.Keys)
        {
            this.KeyStrokes.Add(stroke);
        }

        // Separate KeyDown/KeyUp entries can only come from the advanced mode.
        this.IsAdvancedMode = keySequence.Keys.Any(k => k.EventType != KeyEventType.KeyPress);
        this.UseKernelInjection = keySequence.UseKernelInjection;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new KeySequenceAction
    {
        Keys = this.KeyStrokes.ToList(),
        UseKernelInjection = this.UseKernelInjection
    };

    /// <inheritdoc />
    public override void CancelCapture()
    {
        this.StopKeyStrokeCapture();
        this.StopRecording();
    }

    /// <summary>
    /// Adds a key picked from the list, e.g. F13-F24 or media keys that cannot be captured.
    /// In advanced mode it is added as a KeyDown/KeyUp pair.
    /// </summary>
    public void AddManualKey(KeyHelper.VirtualKeyEntry entry)
    {
        this.KeyStrokes.Add(new KeyStroke
        {
            VirtualKeyCode = entry.VirtualKeyCode,
            Modifiers = ModifierKeys.None,
            DelayAfterMs = KeyPressDelayMs,
            DisplayText = entry.Name,
            EventType = this.IsAdvancedMode ? KeyEventType.KeyDown : KeyEventType.KeyPress
        });

        if (this.IsAdvancedMode)
        {
            this.KeyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = entry.VirtualKeyCode,
                Modifiers = ModifierKeys.None,
                DelayAfterMs = KeyEventDelayMs,
                DisplayText = entry.Name,
                EventType = KeyEventType.KeyUp
            });
        }
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        if (!value && this.isRecording)
        {
            this.StopRecording();
        }
    }

    [RelayCommand]
    private void AddKeyStroke()
    {
        if (this.isCapturingKeyStroke || this.isRecording)
        {
            return;
        }

        this.isCapturingKeyStroke = true;
        this.keyboardHook.IsCapturing = true;
        this.keyboardHook.KeyPressed += this.OnKeyPressedForKeyStroke;

        this.AddKeyButtonText = "Drücke eine Taste...";
        this.IsAddKeyEnabled = false;
    }

    private void StopKeyStrokeCapture()
    {
        if (!this.isCapturingKeyStroke)
        {
            return;
        }

        this.isCapturingKeyStroke = false;
        this.keyboardHook.KeyPressed -= this.OnKeyPressedForKeyStroke;
        this.keyboardHook.IsCapturing = false;

        this.AddKeyButtonText = CaptureKeyText;
        this.IsAddKeyEnabled = true;
    }

    private void OnKeyPressedForKeyStroke(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        this.dispatcherQueue.TryEnqueue(() =>
        {
            this.KeyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = e.VirtualKeyCode,
                Modifiers = e.Modifiers,
                DelayAfterMs = KeyPressDelayMs,
                DisplayText = e.KeyDisplayText
            });
            this.StopKeyStrokeCapture();
        });
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (this.isRecording)
        {
            this.StopRecording();
        }
        else
        {
            this.StartRecording();
        }
    }

    private void StartRecording()
    {
        if (this.isRecording || this.isCapturingKeyStroke)
        {
            return;
        }

        this.isRecording = true;
        this.keyboardHook.IsCapturing = true;

        // Advanced mode records modifier keys as events of their own.
        this.keyboardHook.CaptureModifierKeys = true;
        this.keyboardHook.KeyPressed += this.OnRecordKeyDown;
        this.keyboardHook.KeyReleased += this.OnRecordKeyUp;

        this.RecordButtonText = "Aufnahme stoppen";
        this.RecordButtonGlyph = StopGlyph;
        this.IsRecordingActive = true;
    }

    private void StopRecording()
    {
        if (!this.isRecording)
        {
            return;
        }

        this.isRecording = false;
        this.keyboardHook.KeyPressed -= this.OnRecordKeyDown;
        this.keyboardHook.KeyReleased -= this.OnRecordKeyUp;
        this.keyboardHook.IsCapturing = false;
        this.keyboardHook.CaptureModifierKeys = false;

        this.RecordButtonText = StartRecordingText;
        this.RecordButtonGlyph = RecordGlyph;
        this.IsRecordingActive = false;
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        this.AddRecordedStroke(e.VirtualKeyCode, KeyEventType.KeyDown);
    }

    private void OnRecordKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        this.AddRecordedStroke(e.VirtualKeyCode, KeyEventType.KeyUp);
    }

    private void AddRecordedStroke(int virtualKeyCode, KeyEventType eventType) =>
        this.dispatcherQueue.TryEnqueue(() => this.KeyStrokes.Add(CreateRecordedStroke(virtualKeyCode, eventType)));

    // Modifiers stay empty because advanced mode records them as events of their own.
    private static KeyStroke CreateRecordedStroke(int virtualKeyCode, KeyEventType eventType) => new()
    {
        VirtualKeyCode = virtualKeyCode,
        Modifiers = ModifierKeys.None,
        DelayAfterMs = KeyEventDelayMs,
        DisplayText = KeyHelper.GetKeyName(virtualKeyCode),
        EventType = eventType
    };

    [RelayCommand]
    private void RemoveKeyStroke(KeyStroke? stroke)
    {
        if (stroke is not null)
        {
            this.KeyStrokes.Remove(stroke);
        }
    }
}
