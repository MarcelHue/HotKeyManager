using System.Runtime.InteropServices;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class MidiTriggerEventArgs : EventArgs
{
    public MidiTriggerKind Kind { get; }
    public byte Channel { get; }
    public byte Number { get; }
    public string DeviceName { get; }

    public MidiTriggerEventArgs(MidiTriggerKind kind, byte channel, byte number, string deviceName)
    {
        Kind = kind;
        Channel = channel;
        Number = number;
        DeviceName = deviceName;
    }
}

/// <summary>
/// Lauscht auf allen MIDI-Eingaengen (z.B. Rodecaster Pro II "MIDI function") und meldet
/// Note-On- und Control-Change-Nachrichten.
///
/// Bewusst ueber die klassische WinMM-API statt Windows.Devices.Midi (WinRT):
/// Auf Systemen mit dem neuen Windows-MIDI-Services-Stack (MIDI 2.0, ab Windows 11
/// Insider/2025+) liefert die alte WinRT-API keine Geraete mehr, waehrend WinMM ueber
/// die Kompatibilitaetsschicht weiter funktioniert — und auf aelteren Systemen sowieso.
///
/// Hot-Plug: WinMM hat keine Geraete-Notifications, daher wird alle 5 Sekunden neu
/// gescannt und bei Aenderungen werden die Ports neu geoeffnet.
/// Events kommen auf einem System-Thread — Consumer muessen marshallen.
/// </summary>
public class MidiService : IDisposable
{
    private const int CALLBACK_FUNCTION = 0x00030000;
    private const int MIM_DATA = 0x3C3;
    private const int MMSYSERR_NOERROR = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MIDIINCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwSupport;
    }

    private delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    private static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiInGetDevCapsW")]
    private static extern uint midiInGetDevCaps(UIntPtr deviceId, ref MIDIINCAPS caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint midiInOpen(out IntPtr hMidiIn, uint deviceId, MidiInProc callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern uint midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    private static extern uint midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    private static extern uint midiInClose(IntPtr hMidiIn);

    private readonly object _lock = new();
    private readonly Dictionary<IntPtr, string> _openPorts = new();
    // Delegate als Feld halten, sonst sammelt der GC den nativen Callback ein
    private readonly MidiInProc _callback;
    private Timer? _rescanTimer;
    private string _deviceSignature = string.Empty;
    private bool _disposed;

    /// <summary>Wird fuer jede eingehende Trigger-Nachricht (NoteOn/CC mit Wert &gt; 0) ausgeloest.</summary>
    public event EventHandler<MidiTriggerEventArgs>? MidiReceived;

    /// <summary>Waehrend des Einlernens im Editor true — unterdrueckt das Hotkey-Matching.</summary>
    public bool IsLearning { get; set; }

    public MidiService()
    {
        _callback = OnMidiInProc;
    }

    public void Start()
    {
        if (_rescanTimer != null) return;

        // Sofort scannen, danach alle 5s (Hot-Plug-Erkennung)
        _rescanTimer = new Timer(_ => Rescan(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void Rescan()
    {
        try
        {
            var count = midiInGetNumDevs();
            var names = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                var caps = new MIDIINCAPS();
                if (midiInGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<MIDIINCAPS>()) == MMSYSERR_NOERROR)
                    names.Add(caps.szPname);
                else
                    names.Add($"MIDI-Gerät {i}");
            }

            var signature = string.Join("|", names);

            lock (_lock)
            {
                if (signature == _deviceSignature) return;
                _deviceSignature = signature;

                CloseAllPorts();

                for (uint i = 0; i < count; i++)
                {
                    if (midiInOpen(out var handle, i, _callback, IntPtr.Zero, CALLBACK_FUNCTION) == MMSYSERR_NOERROR
                        && midiInStart(handle) == MMSYSERR_NOERROR)
                    {
                        _openPorts[handle] = names[(int)i];
                        App.Current.LogService.Info($"MIDI-Eingang verbunden: {names[(int)i]}");
                    }
                    else
                    {
                        App.Current.LogService.Warning($"MIDI-Eingang konnte nicht geöffnet werden: {names[(int)i]}");
                    }
                }

                if (count == 0)
                    App.Current.LogService.Info("Keine MIDI-Eingänge mehr vorhanden.");
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Fehler beim MIDI-Geräte-Scan", ex);
        }
    }

    private void OnMidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        if (wMsg != MIM_DATA) return;

        var message = dwParam1.ToInt64();
        var status = (int)(message & 0xF0);
        var channel = (byte)(message & 0x0F);
        var data1 = (byte)((message >> 8) & 0x7F);
        var data2 = (byte)((message >> 16) & 0x7F);

        string deviceName;
        lock (_lock)
        {
            deviceName = _openPorts.TryGetValue(hMidiIn, out var name) ? name : "MIDI";
        }

        switch (status)
        {
            // Note-On mit Velocity 0 ist faktisch Note-Off — nicht ausloesen
            case 0x90 when data2 > 0:
                MidiReceived?.Invoke(this, new MidiTriggerEventArgs(
                    MidiTriggerKind.NoteOn, channel, data1, deviceName));
                break;

            // Wert 0 = "losgelassen" bei Tastern — nur auf Druecken reagieren
            case 0xB0 when data2 > 0:
                MidiReceived?.Invoke(this, new MidiTriggerEventArgs(
                    MidiTriggerKind.ControlChange, channel, data1, deviceName));
                break;
        }
    }

    private void CloseAllPorts()
    {
        foreach (var handle in _openPorts.Keys.ToList())
        {
            try
            {
                midiInStop(handle);
                midiInClose(handle);
            }
            catch { }
        }
        _openPorts.Clear();
    }

    public void Stop()
    {
        _rescanTimer?.Dispose();
        _rescanTimer = null;

        lock (_lock)
        {
            CloseAllPorts();
            _deviceSignature = string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
