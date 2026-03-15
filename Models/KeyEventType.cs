namespace HotKeyManager.Models;

/// <summary>
/// Definiert den Typ eines Tastenevents in einer Tastensequenz.
/// </summary>
public enum KeyEventType
{
    /// <summary>
    /// Automatisch Down + Up (Simple Mode) - Standard-Verhalten
    /// </summary>
    KeyPress,
    
    /// <summary>
    /// Nur Taste drücken (für Kombinationen im Advanced Mode)
    /// </summary>
    KeyDown,
    
    /// <summary>
    /// Nur Taste loslassen (für Kombinationen im Advanced Mode)
    /// </summary>
    KeyUp
}
