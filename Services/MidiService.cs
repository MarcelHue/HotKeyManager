using System.Runtime.InteropServices;
using HotKeyManager.Models;
using static HotKeyManager.Services.Midi.WinMmNative;

namespace HotKeyManager.Services;

/// <summary>
/// Listens on all MIDI inputs (e.g. the Rodecaster Pro II "MIDI function") and reports note-on and
/// control-change messages.
///
/// Uses classic WinMM instead of Windows.Devices.Midi (WinRT) on purpose: with the new Windows MIDI
/// Services stack (MIDI 2.0) the old WinRT API no longer lists any devices, while WinMM keeps working
/// through the compatibility layer, and on older systems anyway.
///
/// WinMM has no device notifications, so inputs are rescanned periodically and reopened on changes.
/// Events arrive on a system thread; consumers must marshal to the UI thread.
/// </summary>
public class MidiService : IDisposable
{
    private const int DataByteShift = 8;
    private const int ValueByteShift = 16;

    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly Dictionary<IntPtr, string> openPorts = new();

    // Kept in a field: otherwise the GC collects the delegate while WinMM still calls it.
    private readonly MidiInProc callback;
    private Timer? rescanTimer;
    private string deviceSignature = string.Empty;
    private bool disposed;

    /// <summary>Prepares the native callback; call <see cref="Start"/> to open the inputs.</summary>
    public MidiService()
    {
        this.callback = this.OnMidiInProc;
    }

    /// <summary>Raised for every trigger message (note-on or CC with a value above 0).</summary>
    public event EventHandler<MidiTriggerEventArgs>? MidiReceived;

    /// <summary>True while the editor learns a trigger; suppresses hotkey matching.</summary>
    public bool IsLearning { get; set; }

    /// <summary>Scans the inputs now and then periodically (hot-plug detection).</summary>
    public void Start()
    {
        if (this.rescanTimer is not null)
        {
            return;
        }

        this.rescanTimer = new Timer(_ => this.Rescan(), null, TimeSpan.Zero, RescanInterval);
    }

    /// <summary>Stops scanning and closes all inputs.</summary>
    public void Stop()
    {
        this.rescanTimer?.Dispose();
        this.rescanTimer = null;

        lock (this.gate)
        {
            this.CloseAllPorts();
            this.deviceSignature = string.Empty;
        }
    }

    /// <summary>Stops listening.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Stop();
        this.disposed = true;
        GC.SuppressFinalize(this);
    }

    private static List<string> ReadInputNames()
    {
        var count = midiInGetNumDevs();
        var names = new List<string>();
        for (uint i = 0; i < count; i++)
        {
            var caps = new MIDIINCAPS();
            var hasCaps = midiInGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<MIDIINCAPS>()) == MMSYSERR_NOERROR;
            names.Add(hasCaps ? caps.szPname : $"MIDI-Gerät {i}");
        }

        return names;
    }

    private void Rescan()
    {
        try
        {
            var names = ReadInputNames();
            var signature = string.Join("|", names);
            lock (this.gate)
            {
                if (signature == this.deviceSignature)
                {
                    return;
                }

                this.deviceSignature = signature;
                this.CloseAllPorts();
                this.OpenAllPorts(names);
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Fehler beim MIDI-Geräte-Scan", ex);
        }
    }

    /// <summary>Must hold <see cref="gate"/>.</summary>
    private void OpenAllPorts(List<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            var result = midiInOpen(out var handle, (uint)i, this.callback, IntPtr.Zero, CALLBACK_FUNCTION);
            var opened = result == MMSYSERR_NOERROR
                && midiInStart(handle) == MMSYSERR_NOERROR;
            if (opened)
            {
                this.openPorts[handle] = names[i];
                App.Current.LogService.Info($"MIDI-Eingang verbunden: {names[i]}");
            }
            else
            {
                App.Current.LogService.Warning($"MIDI-Eingang konnte nicht geöffnet werden: {names[i]}");
            }
        }

        if (names.Count == 0)
        {
            App.Current.LogService.Info("Keine MIDI-Eingänge mehr vorhanden.");
        }
    }

    /// <summary>Must hold <see cref="gate"/>.</summary>
    private void CloseAllPorts()
    {
        foreach (var handle in this.openPorts.Keys.ToList())
        {
            try
            {
                midiInStop(handle);
                midiInClose(handle);
            }
            catch (Exception ex)
            {
                // A device unplugged in the meantime cannot be closed cleanly; the handle is gone anyway.
                App.Current.LogService.Debug($"MIDI-Eingang schließen fehlgeschlagen: {ex.Message}");
            }
        }

        this.openPorts.Clear();
    }

    private void OnMidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        if (wMsg != MIM_DATA)
        {
            return;
        }

        var message = dwParam1.ToInt64();
        var status = (int)(message & STATUS_MASK);
        var channel = (byte)(message & CHANNEL_MASK);
        var data1 = (byte)((message >> DataByteShift) & DATA_MASK);
        var data2 = (byte)((message >> ValueByteShift) & DATA_MASK);

        // Note-on with velocity 0 is a note-off, and CC value 0 is a button release: only presses count.
        var kind = status switch
        {
            NOTE_ON when data2 > 0 => MidiTriggerKind.NoteOn,
            CONTROL_CHANGE when data2 > 0 => MidiTriggerKind.ControlChange,
            _ => (MidiTriggerKind?)null,
        };
        if (kind is null)
        {
            return;
        }

        string deviceName;
        lock (this.gate)
        {
            deviceName = this.openPorts.TryGetValue(hMidiIn, out var name) ? name : "MIDI";
        }

        this.MidiReceived?.Invoke(this, new MidiTriggerEventArgs(kind.Value, channel, data1, deviceName));
    }
}
