using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// ViewModel der Editor-Seite: Name, Tasten-Erfassung, Fenster-Targeting und
/// die austauschbaren Aktionstyp-Editoren.
/// </summary>
public partial class HotkeyEditorViewModel : ObservableObject
{
    private readonly HotkeyManagerService _hotkeyService;
    private readonly KeyboardHookService _keyboardHook;
    private readonly MidiService _midiService;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly HotkeyDefinition? _original;
    private int _capturedKeyCode;
    private ModifierKeys _capturedModifiers;
    private MidiTrigger? _capturedMidi;
    private bool _isCapturingKey;
    private bool _isLearningMidi;
    private DispatcherQueueTimer? _windowCaptureTimer;
    private int _windowCaptureCountdown;

    /// <summary>Wird ausgeloest, wenn die Seite geschlossen werden soll (Speichern/Abbrechen).</summary>
    public event EventHandler? CloseRequested;

    public string PageTitle { get; private set; } = "Neuer Hotkey";

    public IReadOnlyList<ActionEditorViewModelBase> ActionEditors { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Ordner/Gruppe fuer die Listenansicht (leer = ohne Ordner).</summary>
    [ObservableProperty]
    private string _group = string.Empty;

    /// <summary>Bereits vorhandene Ordnernamen als Vorschlaege.</summary>
    public ObservableCollection<string> KnownGroups { get; } = new();

    [ObservableProperty]
    private string _hotkeyDisplayText = "Nicht gesetzt";

    [ObservableProperty]
    private string _captureKeyButtonText = "Taste erfassen";

    [ObservableProperty]
    private bool _isCaptureKeyEnabled = true;

    [ObservableProperty]
    private bool _hasKey;

    [ObservableProperty]
    private string _midiDisplayText = "Nicht gesetzt";

    [ObservableProperty]
    private string _midiLearnButtonText = "MIDI erfassen";

    [ObservableProperty]
    private bool _isMidiLearnEnabled = true;

    [ObservableProperty]
    private bool _hasMidiTrigger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedActionEditor))]
    private int _selectedActionIndex;

    public ActionEditorViewModelBase? SelectedActionEditor =>
        SelectedActionIndex >= 0 && SelectedActionIndex < ActionEditors.Count
            ? ActionEditors[SelectedActionIndex]
            : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindowTargetVisible))]
    private int _windowModeIndex;

    public bool IsWindowTargetVisible => WindowModeIndex != 0;

    [ObservableProperty]
    private string _targetProcessName = string.Empty;

    [ObservableProperty]
    private string _targetWindowTitle = string.Empty;

    [ObservableProperty]
    private bool _isCapturingWindow;

    [ObservableProperty]
    private string _captureWindowButtonText = "Fenster auswählen (3s)";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorOpen;

    public HotkeyEditorViewModel(
        EditorNavArgs? args = null,
        HotkeyManagerService? hotkeyService = null,
        KeyboardHookService? keyboardHook = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        _hotkeyService = hotkeyService ?? App.Current.HotkeyManagerService;
        _keyboardHook = keyboardHook ?? App.Current.KeyboardHookService;
        _midiService = App.Current.MidiService;
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;

        // Seit v1.3.0 ist das Baustein-Makro der einzige Top-Level-Aktionstyp —
        // alle frueheren Aktionstypen existieren als Bausteine darin weiter.
        ActionEditors = new ActionEditorViewModelBase[]
        {
            new MacroEditorViewModel()
        };

        foreach (var known in _hotkeyService.Hotkeys
                     .Select(h => h.Group)
                     .Where(g => !string.IsNullOrWhiteSpace(g))
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase))
        {
            KnownGroups.Add(known);
        }

        if (args?.Hotkey is { } hotkey)
        {
            if (args.IsDuplicate)
            {
                // Kopie: alles uebernehmen ausser dem Ausloeser, als neuer Hotkey speichern
                LoadHotkey(hotkey);
                _capturedKeyCode = 0;
                _capturedModifiers = ModifierKeys.None;
                _capturedMidi = null;
                UpdateHotkeyDisplay();
                UpdateMidiDisplay();
                Name = $"{hotkey.Name} (Kopie)";
                PageTitle = "Hotkey kopieren";
            }
            else
            {
                _original = hotkey;
                LoadHotkey(hotkey);
                PageTitle = "Hotkey bearbeiten";
            }
        }
        else if (args?.PreselectType is { } preselect && preselect != ActionType.Macro)
        {
            // Schnelleinstieg (z.B. "Neues Text-Makro"): passenden Baustein direkt anlegen
            MacroEditor.AddStep(preselect);
        }
    }

    private MacroEditorViewModel MacroEditor => (MacroEditorViewModel)ActionEditors[0];

    private void LoadHotkey(HotkeyDefinition hotkey)
    {
        Name = hotkey.Name;
        Group = hotkey.Group;
        _capturedKeyCode = hotkey.VirtualKeyCode;
        _capturedModifiers = hotkey.Modifiers;
        UpdateHotkeyDisplay();

        _capturedMidi = hotkey.MidiTrigger;
        UpdateMidiDisplay();

        WindowModeIndex = (int)hotkey.WindowMode;
        TargetProcessName = hotkey.TargetProcessName ?? string.Empty;
        TargetWindowTitle = hotkey.TargetWindowTitle ?? string.Empty;

        if (hotkey.Action != null)
        {
            // Noch nicht migrierte Alt-Aktion (z.B. aus altem Import) transparent
            // als Ein-Baustein-Makro laden
            var macro = hotkey.Action as MacroAction
                ?? new MacroAction { Steps = new List<ActionBase> { hotkey.Action } };
            MacroEditor.LoadFrom(macro);
        }
    }

    #region Key Capture

    [RelayCommand]
    private void StartKeyCapture()
    {
        if (_isCapturingKey) return;

        _isCapturingKey = true;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressedForCapture;

        HotkeyDisplayText = "Drücke eine Taste...";
        CaptureKeyButtonText = "Erfassen...";
        IsCaptureKeyEnabled = false;
    }

    private void StopKeyCapture()
    {
        if (!_isCapturingKey) return;

        _isCapturingKey = false;
        _keyboardHook.KeyPressed -= OnKeyPressedForCapture;
        _keyboardHook.IsCapturing = false;

        CaptureKeyButtonText = "Taste erfassen";
        IsCaptureKeyEnabled = true;
        UpdateHotkeyDisplay();
    }

    private void OnKeyPressedForCapture(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        _dispatcherQueue.TryEnqueue(() =>
        {
            _capturedKeyCode = e.VirtualKeyCode;
            _capturedModifiers = e.Modifiers;
            StopKeyCapture();
        });
    }

    [RelayCommand]
    private void ClearKey()
    {
        StopKeyCapture();
        _capturedKeyCode = 0;
        _capturedModifiers = ModifierKeys.None;
        UpdateHotkeyDisplay();
    }

    private void UpdateHotkeyDisplay()
    {
        HasKey = _capturedKeyCode != 0;

        if (_capturedKeyCode == 0)
        {
            HotkeyDisplayText = "Nicht gesetzt";
            return;
        }

        var parts = new List<string>();
        if (_capturedModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (_capturedModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (_capturedModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (_capturedModifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyHelper.GetKeyName(_capturedKeyCode));

        HotkeyDisplayText = string.Join(" + ", parts);
    }

    #endregion

    #region MIDI Learn

    [RelayCommand]
    private void StartMidiLearn()
    {
        if (_isLearningMidi) return;

        _isLearningMidi = true;
        _midiService.IsLearning = true;
        _midiService.MidiReceived += OnMidiReceivedForLearn;

        MidiDisplayText = "Drücke ein Pad / eine Taste am MIDI-Gerät...";
        MidiLearnButtonText = "Erfassen...";
        IsMidiLearnEnabled = false;
    }

    private void StopMidiLearn()
    {
        if (!_isLearningMidi) return;

        _isLearningMidi = false;
        _midiService.MidiReceived -= OnMidiReceivedForLearn;
        _midiService.IsLearning = false;

        MidiLearnButtonText = "MIDI erfassen";
        IsMidiLearnEnabled = true;
        UpdateMidiDisplay();
    }

    private void OnMidiReceivedForLearn(object? sender, MidiTriggerEventArgs e)
    {
        // Kommt auf einem WinRT-Thread an
        _dispatcherQueue.TryEnqueue(() =>
        {
            _capturedMidi = new MidiTrigger
            {
                Kind = e.Kind,
                Channel = e.Channel,
                Number = e.Number
            };
            StopMidiLearn();
        });
    }

    [RelayCommand]
    private void ClearMidi()
    {
        StopMidiLearn();
        _capturedMidi = null;
        UpdateMidiDisplay();
    }

    private void UpdateMidiDisplay()
    {
        HasMidiTrigger = _capturedMidi != null;
        MidiDisplayText = _capturedMidi?.DisplayText ?? "Nicht gesetzt";
    }

    #endregion

    #region Window Capture

    [RelayCommand]
    private void CaptureWindow()
    {
        if (IsCapturingWindow) return;

        IsCapturingWindow = true;
        _windowCaptureCountdown = 3;
        CaptureWindowButtonText = $"Erfasse in {_windowCaptureCountdown}s...";

        _windowCaptureTimer = _dispatcherQueue.CreateTimer();
        _windowCaptureTimer.Interval = TimeSpan.FromSeconds(1);
        _windowCaptureTimer.Tick += (s, e) =>
        {
            _windowCaptureCountdown--;
            if (_windowCaptureCountdown <= 0)
            {
                var windowInfo = WindowHelper.GetActiveWindowInfo();
                if (windowInfo != null)
                {
                    TargetProcessName = windowInfo.ProcessName;
                    TargetWindowTitle = windowInfo.Title;
                }
                StopWindowCapture();
            }
            else
            {
                CaptureWindowButtonText = $"Erfasse in {_windowCaptureCountdown}s...";
            }
        };
        _windowCaptureTimer.Start();
    }

    private void StopWindowCapture()
    {
        _windowCaptureTimer?.Stop();
        _windowCaptureTimer = null;

        IsCapturingWindow = false;
        CaptureWindowButtonText = "Fenster auswählen (3s)";
    }

    #endregion

    /// <summary>Stoppt alle laufenden Captures — von der Page in OnNavigatedFrom aufgerufen.</summary>
    public void CancelAllCaptures()
    {
        StopKeyCapture();
        StopMidiLearn();
        StopWindowCapture();
        foreach (var editor in ActionEditors)
            editor.CancelCapture();
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ShowError("Bitte gib einen Namen ein.");
            return;
        }

        if (_capturedKeyCode == 0 && _capturedMidi == null)
        {
            ShowError("Bitte erfasse eine Tastenkombination oder einen MIDI-Trigger.");
            return;
        }

        // Unvollstaendige Bausteine sind erlaubt — Makros duerfen als Entwurf gespeichert werden
        var action = SelectedActionEditor?.BuildAction();

        var hotkey = new HotkeyDefinition
        {
            Id = _original?.Id ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Group = Group?.Trim() ?? string.Empty,
            VirtualKeyCode = _capturedKeyCode,
            Modifiers = _capturedModifiers,
            MidiTrigger = _capturedMidi,
            IsEnabled = _original?.IsEnabled ?? true,
            WindowMode = (WindowTargetMode)WindowModeIndex,
            TargetProcessName = string.IsNullOrWhiteSpace(TargetProcessName) ? null : TargetProcessName.Trim(),
            TargetWindowTitle = string.IsNullOrWhiteSpace(TargetWindowTitle) ? null : TargetWindowTitle.Trim(),
            Action = action
        };

        if (_original != null)
            _hotkeyService.UpdateHotkey(hotkey);
        else
            _hotkeyService.AddHotkey(hotkey);

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
    }
}
