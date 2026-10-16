using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// ViewModel of the editor page: name, folder, the triggers (<see cref="Key"/>, <see cref="Midi"/>,
/// <see cref="Pad"/>), window targeting and the action editor.
/// </summary>
public partial class HotkeyEditorViewModel : ObservableObject
{
    private readonly HotkeyManagerService hotkeyService;
    private readonly HotkeyDefinition? original;

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Folder in the list view (empty = no folder).</summary>
    [ObservableProperty]
    private string group = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedActionEditor))]
    private int selectedActionIndex;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isErrorOpen;

    /// <summary>Loads the hotkey to edit or duplicate, or prepares a new one.</summary>
    public HotkeyEditorViewModel(
        EditorNavArgs? args = null,
        HotkeyManagerService? hotkeyService = null,
        KeyboardHookService? keyboardHook = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        this.hotkeyService = hotkeyService ?? App.Current.HotkeyManagerService;
        var queue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;
        this.original = args is { Hotkey: { } edited, IsDuplicate: false } ? edited : null;

        this.Key = new KeyCaptureField("Taste erfassen", keyboardHook, queue);
        this.Midi = new MidiTriggerCapture(App.Current.MidiService, queue);
        this.Pad = new PadTriggerCapture(this.hotkeyService, this.original?.Id, this.ShowError);
        this.Window = new WindowTargetPicker(queue);

        // Since v1.3.0 the block macro is the only top-level action type;
        // all earlier action types live on as blocks inside it.
        this.ActionEditors = [new MacroEditorViewModel()];
        this.LoadKnownGroups();

        if (args?.Hotkey is { } hotkey)
        {
            this.LoadHotkey(hotkey, isDuplicate: args.IsDuplicate);
        }
        else if (args?.PreselectType is { } preselect && preselect != ActionType.Macro)
        {
            // Quick start (e.g. "Neues Text-Makro"): create the matching block right away.
            this.MacroEditor.AddStep(preselect);
        }
    }

    /// <summary>Raised when the page should close (save or cancel).</summary>
    public event EventHandler? CloseRequested;

    public string PageTitle { get; private set; } = "Neuer Hotkey";

    public KeyCaptureField Key { get; }

    public MidiTriggerCapture Midi { get; }

    public PadTriggerCapture Pad { get; }

    public WindowTargetPicker Window { get; }

    public IReadOnlyList<ActionEditorViewModelBase> ActionEditors { get; }

    /// <summary>Existing folder names as suggestions.</summary>
    public ObservableCollection<string> KnownGroups { get; } = new();

    public ActionEditorViewModelBase? SelectedActionEditor =>
        this.SelectedActionIndex >= 0 && this.SelectedActionIndex < this.ActionEditors.Count
            ? this.ActionEditors[this.SelectedActionIndex]
            : null;

    private MacroEditorViewModel MacroEditor => (MacroEditorViewModel)this.ActionEditors[0];

    /// <summary>Stops every running capture; called by the page in OnNavigatedFrom.</summary>
    public void CancelAllCaptures()
    {
        this.Key.StopCapture();
        this.Midi.StopLearn();
        this.Pad.StopLearn();
        this.Window.StopCapture();
        foreach (var editor in this.ActionEditors)
        {
            editor.CancelCapture();
        }
    }

    private void LoadKnownGroups()
    {
        var groups = this.hotkeyService.Hotkeys
            .Select(h => h.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase);
        foreach (var known in groups)
        {
            this.KnownGroups.Add(known);
        }
    }

    /// <summary>A duplicate takes over everything except the triggers and is saved as a new hotkey.</summary>
    private void LoadHotkey(HotkeyDefinition hotkey, bool isDuplicate)
    {
        this.Name = isDuplicate ? $"{hotkey.Name} (Kopie)" : hotkey.Name;
        this.Group = hotkey.Group;
        this.PageTitle = isDuplicate ? "Hotkey kopieren" : "Hotkey bearbeiten";
        if (!isDuplicate)
        {
            this.Key.Set(hotkey.VirtualKeyCode, hotkey.Modifiers);
            this.Midi.Set(hotkey.MidiTrigger);
            this.Pad.Set(hotkey.PadTrigger);
        }

        this.Window.Set(hotkey.WindowMode, hotkey.TargetProcessName, hotkey.TargetWindowTitle);
        if (hotkey.Action is not null)
        {
            // An old, not yet migrated action (e.g. from an old import) loads as a one-block macro.
            var macro = hotkey.Action as MacroAction
                ?? new MacroAction { Steps = new List<ActionBase> { hotkey.Action } };
            this.MacroEditor.LoadFrom(macro);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(this.Name))
        {
            this.ShowError("Bitte gib einen Namen ein.");
            return;
        }

        if (!this.Key.IsSet && this.Midi.Trigger is null && this.Pad.Trigger is null)
        {
            this.ShowError("Bitte erfasse eine Tastenkombination, einen MIDI-Trigger oder eine DisplayPad-Taste.");
            return;
        }

        var hotkey = new HotkeyDefinition
        {
            Id = this.original?.Id ?? Guid.NewGuid(),
            Name = this.Name.Trim(),
            Group = this.Group?.Trim() ?? string.Empty,
            VirtualKeyCode = this.Key.VirtualKeyCode,
            Modifiers = this.Key.Modifiers,
            MidiTrigger = this.Midi.Trigger,
            PadTrigger = this.Pad.Trigger,
            IsEnabled = this.original?.IsEnabled ?? true,
            WindowMode = this.Window.Mode,
            TargetProcessName = this.Window.ProcessNameOrNull,
            TargetWindowTitle = this.Window.WindowTitleOrNull,

            // Incomplete blocks are fine: macros may be saved as drafts.
            Action = this.SelectedActionEditor?.BuildAction(),
        };

        if (this.original is not null)
        {
            this.hotkeyService.UpdateHotkey(hotkey);
        }
        else
        {
            this.hotkeyService.AddHotkey(hotkey);
        }

        this.CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => this.CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ShowError(string message)
    {
        this.ErrorMessage = message;
        this.IsErrorOpen = true;
    }
}
