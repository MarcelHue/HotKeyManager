using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor fuer den "MIDI senden"-Baustein — z.B. um den Rodecaster oder eine DAW
/// vom Makro aus zu steuern. Dokumentierte Befehle koennen aus Presets uebernommen werden.
/// </summary>
public partial class MidiSendEditorViewModel : ActionEditorViewModelBase
{
    private readonly MidiPresetService _presetService = new();

    public override string DisplayName => "MIDI senden";
    public override ActionType Type => ActionType.MidiSend;

    /// <summary>
    /// Merkt sich, aus welchem Preset-Befehl die Werte stammen (fuer die Anzeige in der Liste).
    /// Sobald ein Wert von Hand geaendert wird, entfaellt die Zuordnung.
    /// </summary>
    private string? _presetName;
    private string? _presetCommandName;
    private bool _applyingPreset;

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

    /// <summary>Abwechselnd Wert und ZweiterWert senden (An/Aus ohne Rueckmeldung vom Geraet).</summary>
    [ObservableProperty]
    private bool _toggleValues;

    [ObservableProperty]
    private double _valueB;

    [ObservableProperty]
    private double _noteOffAfterMs = 50;

    #region Presets

    public ObservableCollection<MidiPreset> Presets { get; } = new();
    public ObservableCollection<MidiPresetCommand> PresetCommands { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreset))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCommand))]
    private int _presetIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommandHint))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCommand))]
    private int _commandIndex = -1;

    [ObservableProperty]
    private string _presetNotes = string.Empty;

    [ObservableProperty]
    private bool _hasPresetNotes;

    public string CommandHint =>
        CommandIndex >= 0 && CommandIndex < PresetCommands.Count
            ? PresetCommands[CommandIndex].Hint ?? string.Empty
            : string.Empty;

    public bool HasCommandHint => !string.IsNullOrEmpty(CommandHint);

    partial void OnPresetIndexChanged(int value)
    {
        PresetCommands.Clear();
        CommandIndex = -1;

        if (value < 0 || value >= Presets.Count)
        {
            PresetNotes = string.Empty;
            HasPresetNotes = false;
            return;
        }

        var preset = Presets[value];
        foreach (var command in preset.Commands)
            PresetCommands.Add(command);

        PresetNotes = preset.Notes ?? string.Empty;
        HasPresetNotes = !string.IsNullOrEmpty(PresetNotes);
    }

    partial void OnCommandIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CommandHint));

        // Befehl direkt in die Felder uebernehmen
        if (value < 0 || value >= PresetCommands.Count) return;

        var command = PresetCommands[value];

        _applyingPreset = true;
        try
        {
            KindIndex = command.ParsedKind == MidiTriggerKind.NoteOn ? 0 : 1;
            Channel = Math.Clamp(command.Channel, 1, 16);
            Number = Math.Clamp(command.Number, 0, 127);
            Value = Math.Clamp(command.Value, 0, 127);
            ToggleValues = command.Toggle;
            ValueB = Math.Clamp(command.ValueB, 0, 127);
        }
        finally
        {
            _applyingPreset = false;
        }

        _presetName = HasSelectedPreset ? Presets[PresetIndex].Name : null;
        _presetCommandName = command.Name;
    }

    // Werte von Hand geaendert -> Zuordnung zum Preset-Befehl verwerfen
    partial void OnKindIndexChanged(int value) => ForgetPresetLink();
    partial void OnChannelChanged(double value) => ForgetPresetLink();
    partial void OnNumberChanged(double value) => ForgetPresetLink();
    partial void OnValueChanged(double value) => ForgetPresetLink();
    partial void OnValueBChanged(double value) => ForgetPresetLink();
    partial void OnToggleValuesChanged(bool value) => ForgetPresetLink();

    private void ForgetPresetLink()
    {
        if (_applyingPreset) return;
        _presetName = null;
        _presetCommandName = null;
    }

    /// <summary>Aktuelle Einstellungen als Preset-Eintrag.</summary>
    private MidiPresetCommand BuildPresetCommand(string name) => new()
    {
        Name = name,
        Kind = KindIndex == 0 ? "NoteOn" : "ControlChange",
        Channel = Math.Clamp((int)Channel, 1, 16),
        Number = Math.Clamp((int)Number, 0, 127),
        Value = Math.Clamp((int)Value, 0, 127),
        Toggle = ToggleValues,
        ValueB = Math.Clamp((int)ValueB, 0, 127),
        Source = "Selbst getestet",
        Verified = true
    };

    public bool HasSelectedPreset => PresetIndex >= 0 && PresetIndex < Presets.Count;
    public bool HasSelectedCommand => HasSelectedPreset && CommandIndex >= 0 && CommandIndex < PresetCommands.Count;

    /// <summary>
    /// Speichert die aktuellen Einstellungen als neuen Eintrag im gewaehlten Preset
    /// (als verifiziert markiert). Ohne gewaehltes Preset wird ein eigenes angelegt.
    /// </summary>
    public bool SaveAsNewCommand(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var preset = HasSelectedPreset
            ? Presets[PresetIndex]
            : new MidiPreset
            {
                Name = "Eigene Befehle",
                Description = "Selbst getestete MIDI-Befehle."
            };

        preset.Commands.Add(BuildPresetCommand(name.Trim()));

        if (!_presetService.Save(preset)) return false;

        ReloadPresets(preset);
        // Neu angelegten Eintrag gleich auswaehlen
        CommandIndex = PresetCommands.Count - 1;
        return true;
    }

    /// <summary>
    /// Uebernimmt die aktuellen Einstellungen in den gewaehlten Preset-Eintrag
    /// und markiert ihn als verifiziert.
    /// </summary>
    public bool UpdateSelectedCommand()
    {
        if (!HasSelectedCommand) return false;

        var preset = Presets[PresetIndex];
        var index = CommandIndex;
        var existing = PresetCommands[index];

        var updated = BuildPresetCommand(existing.Name);

        // Position im Preset finden (PresetCommands spiegelt preset.Commands)
        if (index < 0 || index >= preset.Commands.Count) return false;
        preset.Commands[index] = updated;

        if (!_presetService.Save(preset)) return false;

        ReloadPresets(preset);
        CommandIndex = index;
        return true;
    }

    public void ReloadPresets(MidiPreset? select = null)
    {
        Presets.Clear();
        foreach (var preset in _presetService.LoadAll())
            Presets.Add(preset);

        if (select == null) return;

        for (int i = 0; i < Presets.Count; i++)
        {
            if (string.Equals(Presets[i].Name, select.Name, StringComparison.OrdinalIgnoreCase))
            {
                PresetIndex = i;
                return;
            }
        }
    }

    /// <summary>Importiert eine Preset-Datei und waehlt sie aus. Liefert false bei ungueltiger Datei.</summary>
    public bool ImportPreset(string path)
    {
        var imported = _presetService.Import(path);
        if (imported == null) return false;

        ReloadPresets(imported);
        return true;
    }

    /// <summary>
    /// Oeffnet die JSON-Datei des gewaehlten Presets im Standardprogramm.
    /// Mitgelieferte Presets werden vorher in den Benutzer-Ordner kopiert, damit
    /// Aenderungen App-Updates ueberleben.
    /// </summary>
    public bool OpenPresetFile()
    {
        if (!HasSelectedPreset) return false;

        var preset = Presets[PresetIndex];

        var isUserCopy = !string.IsNullOrEmpty(preset.FilePath)
            && preset.FilePath!.StartsWith(MidiPresetService.UserFolder, StringComparison.OrdinalIgnoreCase);

        if (!isUserCopy && !_presetService.Save(preset))
            return false;

        if (string.IsNullOrEmpty(preset.FilePath) || !File.Exists(preset.FilePath))
            return false;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = preset.FilePath,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Preset-Datei konnte nicht geöffnet werden", ex);
            return false;
        }
    }

    /// <summary>Liest die Presets neu von der Platte (nach externem Bearbeiten).</summary>
    public void ReloadFromDisk()
    {
        var current = HasSelectedPreset ? Presets[PresetIndex] : null;
        ReloadPresets(current);
    }

    #endregion

    public MidiSendEditorViewModel()
    {
        Devices.Add("Erstes verfügbares Gerät");
        foreach (var name in App.Current.MidiService.GetOutputDeviceNames())
            Devices.Add(name);

        ReloadPresets();
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MidiSendAction midi) return;

        _applyingPreset = true;
        try
        {
            KindIndex = midi.Kind == MidiTriggerKind.NoteOn ? 0 : 1;
            Channel = midi.Channel + 1;
            Number = midi.Number;
            Value = midi.Value;
            ToggleValues = midi.ToggleValues;
            ValueB = midi.ValueB;
            NoteOffAfterMs = midi.NoteOffAfterMs;
        }
        finally
        {
            _applyingPreset = false;
        }

        // Preset-Zuordnung der gespeicherten Aktion erhalten
        _presetName = midi.PresetName;
        _presetCommandName = midi.PresetCommandName;

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

    public override ActionBase BuildAction()
    {
        return new MidiSendAction
        {
            PresetName = _presetName,
            PresetCommandName = _presetCommandName,
            DeviceName = DeviceIndex > 0 && DeviceIndex < Devices.Count ? Devices[DeviceIndex] : string.Empty,
            Kind = KindIndex == 0 ? MidiTriggerKind.NoteOn : MidiTriggerKind.ControlChange,
            Channel = (byte)Math.Clamp((int)Channel - 1, 0, 15),
            Number = (byte)Math.Clamp((int)Number, 0, 127),
            Value = (byte)Math.Clamp((int)Value, 0, 127),
            ToggleValues = ToggleValues,
            ValueB = (byte)Math.Clamp((int)ValueB, 0, 127),
            NoteOffAfterMs = Math.Clamp((int)NoteOffAfterMs, 0, 10_000)
        };
    }

    /// <summary>Sendet die aktuell eingestellte Nachricht sofort — zum Ausprobieren im Editor.</summary>
    [RelayCommand]
    private async Task TestSendAsync()
    {
        if (BuildAction() is MidiSendAction action)
            await App.Current.MidiService.SendAsync(action);
    }
}
