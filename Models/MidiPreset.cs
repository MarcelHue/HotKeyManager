using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>
/// Ein einzelner dokumentierter MIDI-Befehl eines Geraets.
/// </summary>
public class MidiPresetCommand
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"NoteOn" oder "ControlChange".</summary>
    public string Kind { get; set; } = "ControlChange";

    /// <summary>MIDI-Kanal in Anzeige-Zaehlweise (1-16).</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Note- bzw. Controller-Nummer (0-127).</summary>
    public int Number { get; set; }

    /// <summary>Velocity bzw. Controller-Wert (0-127).</summary>
    public int Value { get; set; } = 127;

    /// <summary>Zusatzinfo, z.B. was der Kanal beim Geraet bedeutet.</summary>
    public string? Hint { get; set; }

    /// <summary>Quelle der Information (Doku, Forum, eigene Messung).</summary>
    public string? Source { get; set; }

    /// <summary>True, wenn der Befehl selbst getestet und bestaetigt wurde.</summary>
    public bool Verified { get; set; }

    [JsonIgnore]
    public MidiTriggerKind ParsedKind =>
        Kind.Equals("NoteOn", StringComparison.OrdinalIgnoreCase) ? MidiTriggerKind.NoteOn : MidiTriggerKind.ControlChange;

    /// <summary>Anzeige in der Auswahlliste — ungetestete Befehle sind markiert.</summary>
    [JsonIgnore]
    public string DisplayName => Verified ? Name : $"{Name}  (ungetestet)";
}

/// <summary>
/// Sammlung dokumentierter MIDI-Befehle fuer ein Geraet, als JSON ladbar.
/// Mitgelieferte Presets liegen im App-Ordner unter "Presets\midi",
/// eigene unter %APPDATA%\HotKeyManager\Presets\midi.
/// </summary>
public class MidiPreset
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Voraussetzungen am Geraet, z.B. "MIDI-Steuerung aktivieren".</summary>
    public string? Notes { get; set; }

    public List<MidiPresetCommand> Commands { get; set; } = new();

    [JsonIgnore]
    public string? FilePath { get; set; }
}
