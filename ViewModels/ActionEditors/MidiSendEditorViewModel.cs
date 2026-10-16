using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services.Midi;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor for the "MIDI senden" block, e.g. to control the Rodecaster or a DAW from a macro.
/// Documented commands can be taken over from the presets (<see cref="PresetPicker"/>).
/// </summary>
public partial class MidiSendEditorViewModel : ActionEditorViewModelBase
{
    private const int MinChannel = 1;
    private const int MaxChannel = 16;
    private const int MaxDataValue = 127;
    private const int MaxNoteOffMs = 10_000;
    private const int MiddleC = 60;
    private const int DefaultNoteOffMs = 50;
    private const int NoteKindIndex = 0;
    private const int ControlChangeKindIndex = 1;

    // Which preset command the values came from (shown in the macro list); a manual edit drops the link.
    private string? presetName;
    private string? presetCommandName;
    private bool isApplyingValues;

    [ObservableProperty]
    private int deviceIndex;

    /// <summary>0 = note, 1 = control change.</summary>
    [ObservableProperty]
    private int kindIndex;

    [ObservableProperty]
    private double channel = MinChannel;

    [ObservableProperty]
    private double number = MiddleC;

    [ObservableProperty]
    private double value = MaxDataValue;

    /// <summary>Alternate between Value and ValueB (on/off for devices without feedback).</summary>
    [ObservableProperty]
    private bool toggleValues;

    [ObservableProperty]
    private double valueB;

    [ObservableProperty]
    private double noteOffAfterMs = DefaultNoteOffMs;

    /// <summary>Lists the output devices and loads the presets.</summary>
    public MidiSendEditorViewModel()
    {
        this.Devices.Add("Erstes verfügbares Gerät");
        foreach (var name in MidiOutput.GetDeviceNames())
        {
            this.Devices.Add(name);
        }

        this.PresetPicker.CommandChosen += this.ApplyPresetCommand;
    }

    public override string DisplayName => "MIDI senden";

    public override ActionType Type => ActionType.MidiSend;

    /// <summary>Available output devices; index 0 means "first available device".</summary>
    public ObservableCollection<string> Devices { get; } = new();

    public MidiPresetPicker PresetPicker { get; } = new();

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MidiSendAction midi)
        {
            return;
        }

        this.WithoutDroppingPresetLink(() =>
        {
            this.KindIndex = midi.Kind == MidiTriggerKind.NoteOn ? NoteKindIndex : ControlChangeKindIndex;
            this.Channel = midi.Channel + 1;
            this.Number = midi.Number;
            this.Value = midi.Value;
            this.ToggleValues = midi.ToggleValues;
            this.ValueB = midi.ValueB;
            this.NoteOffAfterMs = midi.NoteOffAfterMs;
        });

        // Keep the preset link of the saved action.
        this.presetName = midi.PresetName;
        this.presetCommandName = midi.PresetCommandName;

        if (!string.IsNullOrWhiteSpace(midi.DeviceName))
        {
            this.DeviceIndex = this.IndexOfDevice(midi.DeviceName);
        }
    }

    public override ActionBase BuildAction() => new MidiSendAction
    {
        PresetName = this.presetName,
        PresetCommandName = this.presetCommandName,
        DeviceName = this.DeviceIndex > 0 && this.DeviceIndex < this.Devices.Count
            ? this.Devices[this.DeviceIndex]
            : string.Empty,
        Kind = this.KindIndex == NoteKindIndex ? MidiTriggerKind.NoteOn : MidiTriggerKind.ControlChange,
        Channel = (byte)Math.Clamp((int)this.Channel - 1, 0, MaxChannel - 1),
        Number = (byte)ClampData(this.Number),
        Value = (byte)ClampData(this.Value),
        ToggleValues = this.ToggleValues,
        ValueB = (byte)ClampData(this.ValueB),
        NoteOffAfterMs = Math.Clamp((int)this.NoteOffAfterMs, 0, MaxNoteOffMs),
    };

    /// <summary>Saves the current values as a new, verified preset command.</summary>
    public bool SaveAsNewCommand(string name) =>
        !string.IsNullOrWhiteSpace(name) && this.PresetPicker.AddCommand(this.BuildPresetCommand(name.Trim()));

    /// <summary>Overwrites the selected preset command with the current values and marks it verified.</summary>
    public bool UpdateSelectedCommand() =>
        this.PresetPicker.SelectedCommand is { } existing
        && this.PresetPicker.ReplaceSelectedCommand(this.BuildPresetCommand(existing.Name));

    private static int ClampData(double data) => Math.Clamp((int)data, 0, MaxDataValue);

    /// <summary>A device that is not plugged in right now is still listed, so saving keeps it.</summary>
    private int IndexOfDevice(string deviceName)
    {
        var index = this.Devices.IndexOf(deviceName);
        if (index >= 0)
        {
            return index;
        }

        this.Devices.Add(deviceName);
        return this.Devices.Count - 1;
    }

    private void ApplyPresetCommand(MidiPresetCommand command)
    {
        this.WithoutDroppingPresetLink(() =>
        {
            this.KindIndex = command.ParsedKind == MidiTriggerKind.NoteOn ? NoteKindIndex : ControlChangeKindIndex;
            this.Channel = Math.Clamp(command.Channel, MinChannel, MaxChannel);
            this.Number = ClampData(command.Number);
            this.Value = ClampData(command.Value);
            this.ToggleValues = command.Toggle;
            this.ValueB = ClampData(command.ValueB);
        });

        this.presetName = this.PresetPicker.SelectedPreset?.Name;
        this.presetCommandName = command.Name;
    }

    private MidiPresetCommand BuildPresetCommand(string name) => new()
    {
        Name = name,
        Kind = this.KindIndex == NoteKindIndex ? "NoteOn" : "ControlChange",
        Channel = Math.Clamp((int)this.Channel, MinChannel, MaxChannel),
        Number = ClampData(this.Number),
        Value = ClampData(this.Value),
        Toggle = this.ToggleValues,
        ValueB = ClampData(this.ValueB),
        Source = "Selbst getestet",
        Verified = true,
    };

    private void WithoutDroppingPresetLink(Action setValues)
    {
        this.isApplyingValues = true;
        try
        {
            setValues();
        }
        finally
        {
            this.isApplyingValues = false;
        }
    }

    private void DropPresetLink()
    {
        if (this.isApplyingValues)
        {
            return;
        }

        this.presetName = null;
        this.presetCommandName = null;
    }

    partial void OnKindIndexChanged(int value) => this.DropPresetLink();

    partial void OnChannelChanged(double value) => this.DropPresetLink();

    partial void OnNumberChanged(double value) => this.DropPresetLink();

    partial void OnValueChanged(double value) => this.DropPresetLink();

    partial void OnValueBChanged(double value) => this.DropPresetLink();

    partial void OnToggleValuesChanged(bool value) => this.DropPresetLink();

    /// <summary>Sends the current message right away, to try it out in the editor.</summary>
    [RelayCommand]
    private Task TestSendAsync() => MidiOutput.SendAsync((MidiSendAction)this.BuildAction());
}
