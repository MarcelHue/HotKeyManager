using System.Runtime.InteropServices;
using HotKeyManager.Models;
using static HotKeyManager.Services.Midi.WinMmNative;

namespace HotKeyManager.Services.Midi;

/// <summary>Sends single MIDI messages (e.g. back to the Rodecaster) through WinMM.</summary>
public static class MidiOutput
{
    private const int DataByteShift = 8;
    private const int ValueByteShift = 16;

    /// <summary>Names of all MIDI output devices, for the device picker in the editor.</summary>
    public static IReadOnlyList<string> GetDeviceNames()
    {
        var names = new List<string>();
        try
        {
            var count = midiOutGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                var caps = new MIDIOUTCAPS();
                if (midiOutGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<MIDIOUTCAPS>()) == MMSYSERR_NOERROR)
                {
                    names.Add(caps.szPname);
                }
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("MIDI-Ausgabegeräte konnten nicht ermittelt werden", ex);
        }

        return names;
    }

    /// <summary>Sends the message of the action to its device (empty name = first available device).</summary>
    public static async Task SendAsync(MidiSendAction action)
    {
        try
        {
            // WinMM needs the device list queried in this process before a port can be opened.
            var names = GetDeviceNames();
            if (names.Count == 0)
            {
                App.Current.LogService.Warning("MIDI senden: kein Ausgabegerät vorhanden.");
                return;
            }

            var index = FindDevice(names, action.DeviceName);
            if (index < 0)
            {
                App.Current.LogService.Warning($"MIDI senden: Gerät '{action.DeviceName}' nicht gefunden.");
                return;
            }

            if (midiOutOpen(out var handle, (uint)index, IntPtr.Zero, IntPtr.Zero, 0) != MMSYSERR_NOERROR)
            {
                App.Current.LogService.Warning($"MIDI senden: Gerät '{names[index]}' konnte nicht geöffnet werden.");
                return;
            }

            try
            {
                await SendMessageAsync(handle, action);
            }
            finally
            {
                midiOutClose(handle);
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("MIDI senden fehlgeschlagen", ex);
        }
    }

    private static int FindDevice(IReadOnlyList<string> names, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return 0;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static async Task SendMessageAsync(IntPtr handle, MidiSendAction action)
    {
        var value = NextValue(action);
        var command = action.Kind == MidiTriggerKind.NoteOn ? NOTE_ON : CONTROL_CHANGE;
        var status = (uint)(command | (action.Channel & CHANNEL_MASK));
        var number = (uint)action.Number << DataByteShift;
        midiOutShortMsg(handle, status | number | ((uint)value << ValueByteShift));

        // Follow up with a note-off (velocity 0) so the note does not hang on the device.
        if (action.Kind == MidiTriggerKind.NoteOn && action.NoteOffAfterMs > 0)
        {
            await Task.Delay(action.NoteOffAfterMs);
            midiOutShortMsg(handle, status | number);
        }
    }

    /// <summary>
    /// In toggle mode Value and ValueB alternate (devices with separate on/off commands whose state
    /// cannot be read back); the last choice is saved so the toggle survives a restart.
    /// </summary>
    private static int NextValue(MidiSendAction action)
    {
        if (!action.ToggleValues)
        {
            return action.Value;
        }

        var value = action.LastSentWasB ? action.Value : action.ValueB;
        action.LastSentWasB = !action.LastSentWasB;
        _ = App.Current.ConfigurationService.SaveAsync();
        return value;
    }
}
