using System.Runtime.InteropServices;

namespace HotKeyManager.Services;

/// <summary>
/// Stummschaltung des Standard-Mikrofons ueber die Windows Core Audio API.
/// (Medientasten laufen dagegen ueber virtuelle Tastencodes im ActionExecutor.)
/// </summary>
public static class AudioService
{
    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        // weitere Methoden werden nicht benoetigt
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        // weitere Methoden werden nicht benoetigt
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float level);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const int CLSCTX_INPROC_SERVER = 1;
    private const int DEVICE_STATE_ACTIVE = 1;

    /// <summary>
    /// Setzt den Mute-Status des Standard-Mikrofons.
    /// mute == null bedeutet umschalten.
    /// </summary>
    public static bool SetMicrophoneMute(bool? mute)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var device) != 0
                || device == null)
            {
                App.Current.LogService.Warning("Kein Standard-Mikrofon gefunden.");
                return false;
            }

            if (device.Activate(AudioEndpointVolumeIid, CLSCTX_INPROC_SERVER, IntPtr.Zero, out var volumeObj) != 0
                || volumeObj is not IAudioEndpointVolume volume)
            {
                App.Current.LogService.Warning("Mikrofon-Lautstärkeregelung nicht verfügbar.");
                return false;
            }

            var context = Guid.Empty;

            bool target;
            if (mute is { } explicitValue)
            {
                target = explicitValue;
            }
            else
            {
                volume.GetMute(out var current);
                target = !current;
            }

            volume.SetMute(target, ref context);
            App.Current.LogService.Debug($"Mikrofon {(target ? "stummgeschaltet" : "aktiviert")}");
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Mikrofon-Stummschaltung fehlgeschlagen", ex);
            return false;
        }
    }
}
