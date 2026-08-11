using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor fuer den "MIDI senden"-Baustein — z.B. um den Rodecaster oder eine DAW
/// vom Makro aus zu steuern.
/// </summary>
public partial class MidiSendEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "MIDI senden";
    public override ActionType Type => ActionType.MidiSend;
    public override string ValidationMessage => "Bitte prüfe die MIDI-Einstellungen.";

    /// <summary>Verfuegbare Ausgabegeraete; Index 0 = "Erstes verfügbares Gerät".</summary>
    public ObservableCollection<string> Devices { get; } = new();

    [ObservableProperty]
    private int _deviceIndex;

    /// <summary>0 = Note, 1 = Control Change.</summary>
    [ObservableProperty]
    private int _kindIndex;

    [ObservableProperty]
    private double _channel = 1;

    [ObservableProperty]
    private double _number = 60;

    [ObservableProperty]
    private double _value = 127;

    [ObservableProperty]
    private double _noteOffAfterMs = 50;

    public MidiSendEditorViewModel()
    {
        Devices.Add("Erstes verfügbares Gerät");
        foreach (var name in App.Current.MidiService.GetOutputDeviceNames())
            Devices.Add(name);
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MidiSendAction midi) return;

        KindIndex = midi.Kind == MidiTriggerKind.NoteOn ? 0 : 1;
        Channel = midi.Channel + 1;
        Number = midi.Number;
        Value = midi.Value;
        NoteOffAfterMs = midi.NoteOffAfterMs;

        if (!string.IsNullOrWhiteSpace(midi.DeviceName))
        {
            var index = Devices.IndexOf(midi.DeviceName);
            if (index < 0)
            {
                // Geraet aktuell nicht angeschlossen — trotzdem anzeigen, damit die
                // Zuordnung beim Speichern erhalten bleibt
                Devices.Add(midi.DeviceName);
                index = Devices.Count - 1;
            }
            DeviceIndex = index;
        }
    }

    public override ActionBase? BuildAction()
    {
        return new MidiSendAction
        {
            DeviceName = DeviceIndex > 0 && DeviceIndex < Devices.Count ? Devices[DeviceIndex] : string.Empty,
            Kind = KindIndex == 0 ? MidiTriggerKind.NoteOn : MidiTriggerKind.ControlChange,
            Channel = (byte)Math.Clamp((int)Channel - 1, 0, 15),
            Number = (byte)Math.Clamp((int)Number, 0, 127),
            Value = (byte)Math.Clamp((int)Value, 0, 127),
            NoteOffAfterMs = Math.Clamp((int)NoteOffAfterMs, 0, 10_000)
        };
    }
}
