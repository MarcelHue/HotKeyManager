using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Captures or clears a key or MIDI trigger right in the hotkey list, without opening the editor.
/// Only one row captures at a time; starting another capture cancels the running one.
/// </summary>
public sealed class ListTriggerCapture
{
    private readonly KeyboardHookService keyboardHook;
    private readonly MidiService midiService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Action saveChanges;
    private HotkeyDefinition? capturingHotkey;

    /// <summary>Creates the capture for the list page.</summary>
    /// <param name="saveChanges">Persists in-place edits of the hotkeys.</param>
    public ListTriggerCapture(
        KeyboardHookService keyboardHook,
        MidiService midiService,
        DispatcherQueue dispatcherQueue,
        Action saveChanges)
    {
        this.keyboardHook = keyboardHook;
        this.midiService = midiService;
        this.dispatcherQueue = dispatcherQueue;
        this.saveChanges = saveChanges;
    }

    /// <summary>The key may only go if another trigger keeps the hotkey reachable.</summary>
    public static bool CanClearKey(HotkeyDefinition hotkey) =>
        hotkey.MidiTrigger is not null || hotkey.PadTrigger is not null;

    /// <summary>The MIDI trigger may only go if another trigger keeps the hotkey reachable.</summary>
    public static bool CanClearMidi(HotkeyDefinition hotkey) =>
        hotkey.VirtualKeyCode != 0 || hotkey.PadTrigger is not null;

    /// <summary>Waits for the next key combination and stores it as the row's key trigger.</summary>
    public void StartKey(HotkeyDefinition hotkey)
    {
        this.Begin(hotkey, "Drücke eine Taste…");
        this.keyboardHook.IsCapturing = true;
        this.keyboardHook.KeyPressed += this.OnKeyPressed;
    }

    /// <summary>Waits for the next MIDI message and stores it as the row's MIDI trigger.</summary>
    public void StartMidi(HotkeyDefinition hotkey)
    {
        this.Begin(hotkey, "Drücke ein Pad am MIDI-Gerät…");
        this.midiService.IsLearning = true;
        this.midiService.MidiReceived += this.OnMidiReceived;
    }

    /// <summary>Removes the key trigger if the hotkey keeps another trigger.</summary>
    public void ClearKey(HotkeyDefinition hotkey)
    {
        this.Cancel();
        if (!CanClearKey(hotkey))
        {
            return;
        }

        hotkey.VirtualKeyCode = 0;
        hotkey.Modifiers = ModifierKeys.None;
        this.saveChanges();
    }

    /// <summary>Removes the MIDI trigger if the hotkey keeps another trigger.</summary>
    public void ClearMidi(HotkeyDefinition hotkey)
    {
        this.Cancel();
        if (!CanClearMidi(hotkey))
        {
            return;
        }

        hotkey.MidiTrigger = null;
        this.saveChanges();
    }

    /// <summary>Cancels a running capture and releases the keyboard hook and MIDI input.</summary>
    public void Cancel()
    {
        if (this.capturingHotkey is null)
        {
            return;
        }

        this.keyboardHook.KeyPressed -= this.OnKeyPressed;
        this.keyboardHook.IsCapturing = false;
        this.midiService.MidiReceived -= this.OnMidiReceived;
        this.midiService.IsLearning = false;

        this.capturingHotkey.CaptureHint = null;
        this.capturingHotkey = null;
    }

    private void Begin(HotkeyDefinition hotkey, string hint)
    {
        this.Cancel();
        this.capturingHotkey = hotkey;
        hotkey.CaptureHint = hint;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        this.Complete(hotkey =>
        {
            hotkey.VirtualKeyCode = e.VirtualKeyCode;
            hotkey.Modifiers = e.Modifiers;
        });
    }

    private void OnMidiReceived(object? sender, MidiTriggerEventArgs e) =>
        this.Complete(hotkey => hotkey.MidiTrigger = new MidiTrigger
        {
            Kind = e.Kind,
            Channel = e.Channel,
            Number = e.Number,
        });

    /// <summary>Hook and MIDI events arrive on background threads; the row is updated on the UI thread.</summary>
    private void Complete(Action<HotkeyDefinition> apply) =>
        this.dispatcherQueue.TryEnqueue(() =>
        {
            if (this.capturingHotkey is not { } hotkey)
            {
                return;
            }

            apply(hotkey);
            this.Cancel();
            this.saveChanges();
        });
}
