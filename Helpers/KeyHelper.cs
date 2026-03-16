using System.Runtime.InteropServices;

namespace HotKeyManager.Helpers;

public static class KeyHelper
{
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint MAPVK_VK_TO_CHAR = 2;
    // Virtual Key Codes
    public static class VK
    {
        public const int VK_BACK = 0x08;
        public const int VK_TAB = 0x09;
        public const int VK_RETURN = 0x0D;
        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12; // Alt
        public const int VK_PAUSE = 0x13;
        public const int VK_CAPITAL = 0x14;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_SPACE = 0x20;
        public const int VK_PRIOR = 0x21; // Page Up
        public const int VK_NEXT = 0x22;  // Page Down
        public const int VK_END = 0x23;
        public const int VK_HOME = 0x24;
        public const int VK_LEFT = 0x25;
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;
        public const int VK_SNAPSHOT = 0x2C; // Print Screen
        public const int VK_INSERT = 0x2D;
        public const int VK_DELETE = 0x2E;
        
        // Numbers 0-9: 0x30-0x39
        // Letters A-Z: 0x41-0x5A
        
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const int VK_APPS = 0x5D;
        public const int VK_SLEEP = 0x5F;
        
        // Numpad
        public const int VK_NUMPAD0 = 0x60;
        public const int VK_NUMPAD1 = 0x61;
        public const int VK_NUMPAD2 = 0x62;
        public const int VK_NUMPAD3 = 0x63;
        public const int VK_NUMPAD4 = 0x64;
        public const int VK_NUMPAD5 = 0x65;
        public const int VK_NUMPAD6 = 0x66;
        public const int VK_NUMPAD7 = 0x67;
        public const int VK_NUMPAD8 = 0x68;
        public const int VK_NUMPAD9 = 0x69;
        public const int VK_MULTIPLY = 0x6A;
        public const int VK_ADD = 0x6B;
        public const int VK_SEPARATOR = 0x6C;
        public const int VK_SUBTRACT = 0x6D;
        public const int VK_DECIMAL = 0x6E;
        public const int VK_DIVIDE = 0x6F;
        
        // Function Keys F1-F24
        public const int VK_F1 = 0x70;
        public const int VK_F2 = 0x71;
        public const int VK_F3 = 0x72;
        public const int VK_F4 = 0x73;
        public const int VK_F5 = 0x74;
        public const int VK_F6 = 0x75;
        public const int VK_F7 = 0x76;
        public const int VK_F8 = 0x77;
        public const int VK_F9 = 0x78;
        public const int VK_F10 = 0x79;
        public const int VK_F11 = 0x7A;
        public const int VK_F12 = 0x7B;
        public const int VK_F13 = 0x7C;
        public const int VK_F14 = 0x7D;
        public const int VK_F15 = 0x7E;
        public const int VK_F16 = 0x7F;
        public const int VK_F17 = 0x80;
        public const int VK_F18 = 0x81;
        public const int VK_F19 = 0x82;
        public const int VK_F20 = 0x83;
        public const int VK_F21 = 0x84;
        public const int VK_F22 = 0x85;
        public const int VK_F23 = 0x86;
        public const int VK_F24 = 0x87;
        
        public const int VK_NUMLOCK = 0x90;
        public const int VK_SCROLL = 0x91;
        
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_LMENU = 0xA4;
        public const int VK_RMENU = 0xA5;
        
        public const int VK_OEM_1 = 0xBA;
        public const int VK_OEM_PLUS = 0xBB;
        public const int VK_OEM_COMMA = 0xBC;
        public const int VK_OEM_MINUS = 0xBD;
        public const int VK_OEM_PERIOD = 0xBE;
        public const int VK_OEM_2 = 0xBF;
        public const int VK_OEM_3 = 0xC0;
        public const int VK_OEM_4 = 0xDB;
        public const int VK_OEM_5 = 0xDC;
        public const int VK_OEM_6 = 0xDD;
        public const int VK_OEM_7 = 0xDE;
        public const int VK_OEM_102 = 0xE2;
    }
    
    private static readonly Dictionary<int, string> KeyNames = new()
    {
        { VK.VK_BACK, "Backspace" },
        { VK.VK_TAB, "Tab" },
        { VK.VK_RETURN, "Enter" },
        { VK.VK_PAUSE, "Pause" },
        { VK.VK_CAPITAL, "Caps Lock" },
        { VK.VK_ESCAPE, "Esc" },
        { VK.VK_SPACE, "Space" },
        { VK.VK_PRIOR, "Page Up" },
        { VK.VK_NEXT, "Page Down" },
        { VK.VK_END, "End" },
        { VK.VK_HOME, "Home" },
        { VK.VK_LEFT, "←" },
        { VK.VK_UP, "↑" },
        { VK.VK_RIGHT, "→" },
        { VK.VK_DOWN, "↓" },
        { VK.VK_SNAPSHOT, "Print Screen" },
        { VK.VK_INSERT, "Insert" },
        { VK.VK_DELETE, "Delete" },
        { VK.VK_LWIN, "Win" },
        { VK.VK_RWIN, "Win" },
        { VK.VK_APPS, "Menu" },
        { VK.VK_NUMPAD0, "Num 0" },
        { VK.VK_NUMPAD1, "Num 1" },
        { VK.VK_NUMPAD2, "Num 2" },
        { VK.VK_NUMPAD3, "Num 3" },
        { VK.VK_NUMPAD4, "Num 4" },
        { VK.VK_NUMPAD5, "Num 5" },
        { VK.VK_NUMPAD6, "Num 6" },
        { VK.VK_NUMPAD7, "Num 7" },
        { VK.VK_NUMPAD8, "Num 8" },
        { VK.VK_NUMPAD9, "Num 9" },
        { VK.VK_MULTIPLY, "Num *" },
        { VK.VK_ADD, "Num +" },
        { VK.VK_SUBTRACT, "Num -" },
        { VK.VK_DECIMAL, "Num ." },
        { VK.VK_DIVIDE, "Num /" },
        { VK.VK_F1, "F1" },
        { VK.VK_F2, "F2" },
        { VK.VK_F3, "F3" },
        { VK.VK_F4, "F4" },
        { VK.VK_F5, "F5" },
        { VK.VK_F6, "F6" },
        { VK.VK_F7, "F7" },
        { VK.VK_F8, "F8" },
        { VK.VK_F9, "F9" },
        { VK.VK_F10, "F10" },
        { VK.VK_F11, "F11" },
        { VK.VK_F12, "F12" },
        { VK.VK_F13, "F13" },
        { VK.VK_F14, "F14" },
        { VK.VK_F15, "F15" },
        { VK.VK_F16, "F16" },
        { VK.VK_F17, "F17" },
        { VK.VK_F18, "F18" },
        { VK.VK_F19, "F19" },
        { VK.VK_F20, "F20" },
        { VK.VK_F21, "F21" },
        { VK.VK_F22, "F22" },
        { VK.VK_F23, "F23" },
        { VK.VK_F24, "F24" },
        { VK.VK_NUMLOCK, "Num Lock" },
        { VK.VK_SCROLL, "Scroll Lock" },
    };
    
    public static string GetKeyName(int virtualKeyCode)
    {
        if (KeyNames.TryGetValue(virtualKeyCode, out var name))
            return name;
        
        // Numbers 0-9
        if (virtualKeyCode is >= 0x30 and <= 0x39)
            return ((char)virtualKeyCode).ToString();
        
        // Letters A-Z
        if (virtualKeyCode is >= 0x41 and <= 0x5A)
            return ((char)virtualKeyCode).ToString();
        
        // OEM and other keys: use keyboard-layout-aware mapping
        var mapped = MapVirtualKey((uint)virtualKeyCode, MAPVK_VK_TO_CHAR);
        if (mapped > 0 && mapped < 0x10000)
        {
            var ch = (char)(mapped & 0x7FFFFFFF); // strip dead-key flag (bit 31)
            if (!char.IsControl(ch))
                return ch.ToString().ToUpper();
        }

        return $"Key {virtualKeyCode:X2}";
    }
    
    public static bool IsModifierKey(int virtualKeyCode)
    {
        return virtualKeyCode is VK.VK_SHIFT or VK.VK_CONTROL or VK.VK_MENU 
            or VK.VK_LSHIFT or VK.VK_RSHIFT 
            or VK.VK_LCONTROL or VK.VK_RCONTROL 
            or VK.VK_LMENU or VK.VK_RMENU
            or VK.VK_LWIN or VK.VK_RWIN;
    }
}
