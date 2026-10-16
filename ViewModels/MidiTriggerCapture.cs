using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>MIDI trigger of a hotkey: learns the next note-on or CC from any MIDI input.</summary>
public partial class MidiTriggerCapture : ObservableObject
{
    private const string NotSetText = "Nicht gesetzt";
    private const string IdleButtonText = "MIDI erfassen";

    private readonly MidiService midiService;
    private readonly DispatcherQueue dispatcherQueue;
    private bool isLearning;

    [ObservableProperty]
    private string displayText = NotSetText;

    [ObservableProperty]
    private string buttonText = IdleButtonText;

    [ObservableProperty]
    private bool isButtonEnabled = true;

    [ObservableProperty]
    private bool hasTrigger;

    /// <summary>Creates the capture for one editor page.</summary>
    public MidiTriggerCapture(MidiService midiService, DispatcherQueue dispatcherQueue)
    {
        this.midiService = midiService;
        this.dispatcherQueue = dispatcherQueue;
    }

    public MidiTrigger? Trigger { get; private set; }

    /// <summary>Shows an existing trigger (or none).</summary>
    public void Set(MidiTrigger? trigger)
    {
        this.Trigger = trigger;
        this.UpdateDisplay();
    }

    /// <summary>Stops a running learn and keeps the previous trigger.</summary>
    public void StopLearn()
    {
        if (!this.isLearning)
        {
            return;
        }

        this.isLearning = false;
        this.midiService.MidiReceived -= this.OnMidiReceived;
        this.midiService.IsLearning = false;

        this.ButtonText = IdleButtonText;
        this.IsButtonEnabled = true;
        this.UpdateDisplay();
    }

    [RelayCommand]
    private void StartLearn()
    {
        if (this.isLearning)
        {
            return;
        }

        // While learning, the service must not fire hotkeys for the incoming message.
        this.isLearning = true;
        this.midiService.IsLearning = true;
        this.midiService.MidiReceived += this.OnMidiReceived;

        this.DisplayText = "Drücke ein Pad / eine Taste am MIDI-Gerät...";
        this.ButtonText = "Erfassen...";
        this.IsButtonEnabled = false;
    }

    /// <summary>Arrives on a WinMM thread, so the result is applied on the UI thread.</summary>
    private void OnMidiReceived(object? sender, MidiTriggerEventArgs e) =>
        this.dispatcherQueue.TryEnqueue(() =>
        {
            this.Trigger = new MidiTrigger { Kind = e.Kind, Channel = e.Channel, Number = e.Number };
            this.StopLearn();
        });

    [RelayCommand]
    private void Clear()
    {
        this.StopLearn();
        this.Set(null);
    }

    private void UpdateDisplay()
    {
        this.HasTrigger = this.Trigger is not null;
        this.DisplayText = this.Trigger?.DisplayText ?? NotSetText;
    }
}
