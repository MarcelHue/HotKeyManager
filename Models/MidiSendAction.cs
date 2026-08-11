namespace HotKeyManager.Models;

/// <summary>
/// Sendet eine MIDI-Nachricht an ein Ausgabegeraet (z.B. zurueck an den Rodecaster,
/// an eine DAW oder ein virtuelles MIDI-Kabel).
/// </summary>
public class MidiSendAction : ActionBase
{
    public override ActionType Type => ActionType.MidiSend;
    public override string DisplayName => "MIDI senden";
    public override string Description =>
        ToggleValues
            ? $"{(Kind == MidiTriggerKind.NoteOn ? "Note" : "CC")} {Number} · Wert {Value} ↔ {ValueB} · Kanal {Channel + 1}"
            : $"{(Kind == MidiTriggerKind.NoteOn ? "Note" : "CC")} {Number} · Wert {Value} · Kanal {Channel + 1}";

    /// <summary>Name des MIDI-Ausgabegeraets. Leer = erstes verfuegbares Geraet.</summary>
    public string DeviceName { get; set; } = string.Empty;

    public MidiTriggerKind Kind { get; set; } = MidiTriggerKind.NoteOn;

    /// <summary>MIDI-Kanal 0-15 (Anzeige: 1-16).</summary>
    public byte Channel { get; set; }

    /// <summary>Note-Nummer bzw. Controller-Nummer, 0-127.</summary>
    public byte Number { get; set; } = 60;

    /// <summary>Velocity (NoteOn) bzw. Controller-Wert (CC), 0-127.</summary>
    public byte Value { get; set; } = 127;

    /// <summary>
    /// Wenn true, wird bei jeder Ausfuehrung abwechselnd Value und ValueB gesendet.
    /// Fuer Geraete, die getrennte Befehle fuer An/Aus haben (z.B. Rodecaster:
    /// CC 27 Wert 1 = stumm, Wert 0 = laut) und deren Zustand sich nicht auslesen laesst.
    /// </summary>
    public bool ToggleValues { get; set; }

    /// <summary>Zweiter Wert fuer den Umschalt-Modus.</summary>
    public byte ValueB { get; set; }

    /// <summary>
    /// Merkt sich, welcher Wert zuletzt gesendet wurde (damit das Umschalten
    /// auch nach einem Neustart weiterlaeuft). Wird beim Ausfuehren aktualisiert.
    /// </summary>
    public bool LastSentWasB { get; set; }

    /// <summary>Bei NoteOn: passendes Note-Off nach dieser Zeit senden (0 = keins).</summary>
    public int NoteOffAfterMs { get; set; } = 50;
}
