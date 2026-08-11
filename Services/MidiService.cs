using HotKeyManager.Models;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;

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
/// Lauscht auf allen MIDI-Eingaengen (z.B. Rodecaster Pro II "MIDI Function") und meldet
/// Note-On- und Control-Change-Nachrichten. Geraete werden per DeviceWatcher automatisch
/// erkannt (Hot-Plug). Events kommen auf einem WinRT-Thread — Consumer muessen marshallen.
/// </summary>
public class MidiService : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (MidiInPort Port, string Name)> _openPorts = new();
    private DeviceWatcher? _watcher;
    private bool _disposed;

    /// <summary>Wird fuer jede eingehende Trigger-Nachricht (NoteOn/CC mit Wert &gt; 0) ausgeloest.</summary>
    public event EventHandler<MidiTriggerEventArgs>? MidiReceived;

    /// <summary>Waehrend des Einlernens im Editor true — unterdrueckt das Hotkey-Matching.</summary>
    public bool IsLearning { get; set; }

    public void Start()
    {
        if (_watcher != null) return;

        _watcher = DeviceInformation.CreateWatcher(MidiInPort.GetDeviceSelector());
        _watcher.Added += (s, info) => _ = OpenPortAsync(info.Id, info.Name);
        _watcher.Removed += (s, update) => ClosePort(update.Id);
        _watcher.Start();
    }

    private async Task OpenPortAsync(string id, string name)
    {
        try
        {
            var port = await MidiInPort.FromIdAsync(id);
            if (port == null)
            {
                App.Current.LogService.Warning($"MIDI-Eingang konnte nicht geöffnet werden: {name}");
                return;
            }

            port.MessageReceived += OnMessageReceived;

            lock (_lock)
            {
                _openPorts[id] = (port, name);
            }

            App.Current.LogService.Info($"MIDI-Eingang verbunden: {name}");
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning($"Fehler beim Öffnen des MIDI-Eingangs {name}", ex);
        }
    }

    private void ClosePort(string id)
    {
        lock (_lock)
        {
            if (_openPorts.Remove(id, out var entry))
            {
                try
                {
                    entry.Port.MessageReceived -= OnMessageReceived;
                    entry.Port.Dispose();
                }
                catch { }
                App.Current.LogService.Info($"MIDI-Eingang getrennt: {entry.Name}");
            }
        }
    }

    private void OnMessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
    {
        string deviceName;
        lock (_lock)
        {
            deviceName = _openPorts.FirstOrDefault(p => ReferenceEquals(p.Value.Port, sender)).Value.Name ?? "MIDI";
        }

        switch (args.Message)
        {
            // Velocity 0 ist faktisch Note-Off — nicht ausloesen
            case MidiNoteOnMessage note when note.Velocity > 0:
                MidiReceived?.Invoke(this, new MidiTriggerEventArgs(
                    MidiTriggerKind.NoteOn, note.Channel, note.Note, deviceName));
                break;

            // Wert 0 = "losgelassen" bei Tastern — nur auf Druecken reagieren
            case MidiControlChangeMessage cc when cc.ControlValue > 0:
                MidiReceived?.Invoke(this, new MidiTriggerEventArgs(
                    MidiTriggerKind.ControlChange, cc.Channel, cc.Controller, deviceName));
                break;
        }
    }

    public void Stop()
    {
        _watcher?.Stop();
        _watcher = null;

        lock (_lock)
        {
            foreach (var (port, _) in _openPorts.Values)
            {
                try
                {
                    port.MessageReceived -= OnMessageReceived;
                    port.Dispose();
                }
                catch { }
            }
            _openPorts.Clear();
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
