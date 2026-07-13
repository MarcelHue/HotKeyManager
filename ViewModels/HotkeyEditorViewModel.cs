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
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly HotkeyDefinition? _original;
    private int _capturedKeyCode;
    private ModifierKeys _capturedModifiers;
    private bool _isCapturingKey;
    private DispatcherQueueTimer? _windowCaptureTimer;
    private int _windowCaptureCountdown;

    /// <summary>Wird ausgeloest, wenn die Seite geschlossen werden soll (Speichern/Abbrechen).</summary>
    public event EventHandler? CloseRequested;

    public string PageTitle => _original == null ? "Neuer Hotkey" : "Hotkey bearbeiten";

    public IReadOnlyList<ActionEditorViewModelBase> ActionEditors { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _hotkeyDisplayText = "Nicht gesetzt";

    [ObservableProperty]
    private string _captureKeyButtonText = "Taste erfassen";

    [ObservableProperty]
    private bool _isCaptureKeyEnabled = true;

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
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;

        ActionEditors = new ActionEditorViewModelBase[]
        {
            new WebhookEditorViewModel(),
            new KeySequenceEditorViewModel(),
            new ProcessEditorViewModel(),
            new BatchEditorViewModel(),
            new SendTextEditorViewModel()
        };

        if (args?.Hotkey is { } hotkey)
        {
            _original = hotkey;
            LoadHotkey(hotkey);
        }
        else if (args?.PreselectType is { } preselect)
        {
            SelectIndexFor(preselect);
        }
    }

    private void LoadHotkey(HotkeyDefinition hotkey)
    {
        Name = hotkey.Name;
        _capturedKeyCode = hotkey.VirtualKeyCode;
        _capturedModifiers = hotkey.Modifiers;
        UpdateHotkeyDisplay();

        WindowModeIndex = (int)hotkey.WindowMode;
        TargetProcessName = hotkey.TargetProcessName ?? string.Empty;
        TargetWindowTitle = hotkey.TargetWindowTitle ?? string.Empty;

        if (hotkey.Action != null)
        {
            SelectIndexFor(hotkey.Action.Type);
            SelectedActionEditor?.LoadFrom(hotkey.Action);
        }
    }

    private void SelectIndexFor(ActionType type)
    {
        for (int i = 0; i < ActionEditors.Count; i++)
        {
            if (ActionEditors[i].Type == type)
            {
                SelectedActionIndex = i;
                return;
            }
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

    private void UpdateHotkeyDisplay()
    {
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

        if (_capturedKeyCode == 0)
        {
            ShowError("Bitte erfasse eine Tastenkombination.");
            return;
        }

        var editor = SelectedActionEditor;
        var action = editor?.BuildAction();
        if (action == null)
        {
            ShowError(editor?.ValidationMessage ?? "Bitte konfiguriere die Aktion.");
            return;
        }

        var hotkey = new HotkeyDefinition
        {
            Id = _original?.Id ?? Guid.NewGuid(),
            Name = Name.Trim(),
            VirtualKeyCode = _capturedKeyCode,
            Modifiers = _capturedModifiers,
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
