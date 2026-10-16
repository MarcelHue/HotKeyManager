using System.Runtime.InteropServices;

namespace HotKeyManager.Services.Midi;

/// <summary>WinMM MIDI functions; names follow the Windows API.</summary>
internal static class WinMmNative
{
    public const int CALLBACK_FUNCTION = 0x00030000;
    public const int MIM_DATA = 0x3C3;
    public const int MMSYSERR_NOERROR = 0;

    public const int STATUS_MASK = 0xF0;
    public const int CHANNEL_MASK = 0x0F;
    public const int DATA_MASK = 0x7F;
    public const int NOTE_ON = 0x90;
    public const int CONTROL_CHANGE = 0xB0;

    public delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    public static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiInGetDevCapsW")]
    public static extern uint midiInGetDevCaps(UIntPtr deviceId, ref MIDIINCAPS caps, uint size);

    [DllImport("winmm.dll")]
    public static extern uint midiInOpen(
        out IntPtr hMidiIn, uint deviceId, MidiInProc callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    public static extern uint midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiInClose(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiOutGetDevCapsW")]
    public static extern uint midiOutGetDevCaps(UIntPtr deviceId, ref MIDIOUTCAPS caps, uint size);

    [DllImport("winmm.dll")]
    public static extern uint midiOutOpen(
        out IntPtr hMidiOut, uint deviceId, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    public static extern uint midiOutShortMsg(IntPtr hMidiOut, uint message);

    [DllImport("winmm.dll")]
    public static extern uint midiOutClose(IntPtr hMidiOut);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MIDIINCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwSupport;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MIDIOUTCAPS
    {
        public ushort wMid;
        public ushort wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public ushort wTechnology;
        public ushort wVoices;
        public ushort wNotes;
        public ushort wChannelMask;
        public uint dwSupport;
    }
}
