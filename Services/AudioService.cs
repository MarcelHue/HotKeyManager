using System.Runtime.InteropServices;

namespace HotKeyManager.Services;

/// <summary>
/// Mutes the default microphone through the Windows Core Audio API.
/// Media keys do not need this; they are sent as virtual key codes by the ActionExecutor.
/// </summary>
public static class AudioService
{
    private const int CLSCTX_INPROC_SERVER = 1;
    private const int S_OK = 0;

    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    // COM interfaces are called by vtable slot, so every method up to the last one used must be
    // declared in order. Methods after that are left out.
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(
            [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
            int clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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

    /// <summary>Sets the mute state of the default communications microphone.</summary>
    /// <param name="mute">True to mute, false to unmute, null to toggle.</param>
    /// <returns>False when no microphone was found or the call failed (already logged).</returns>
    public static bool SetMicrophoneMute(bool? mute)
    {
        try
        {
            var volume = GetMicrophoneVolume();
            if (volume is null)
            {
                return false;
            }

            var context = Guid.Empty;
            var target = mute ?? !IsMuted(volume);
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

    private static IAudioEndpointVolume? GetMicrophoneVolume()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        var result = enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var device);
        if ((result != S_OK) || (device is null))
        {
            App.Current.LogService.Warning("Kein Standard-Mikrofon gefunden.");
            return null;
        }

        result = device.Activate(AudioEndpointVolumeIid, CLSCTX_INPROC_SERVER, IntPtr.Zero, out var volumeObject);
        if ((result != S_OK) || (volumeObject is not IAudioEndpointVolume volume))
        {
            App.Current.LogService.Warning("Mikrofon-Lautstärkeregelung nicht verfügbar.");
            return null;
        }

        return volume;
    }

    private static bool IsMuted(IAudioEndpointVolume volume)
    {
        volume.GetMute(out var isMuted);
        return isMuted;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }
}
