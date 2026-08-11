using System.Text.Json;
using HotKeyManager.Models;

namespace HotKeyManager.Helpers;

/// <summary>
/// Tiefe Kopie von Aktionen ueber einen JSON-Roundtrip — nutzt die vorhandene
/// $type-Polymorphie, damit auch verschachtelte Makro-Bausteine korrekt kopiert werden.
/// </summary>
public static class ActionCloner
{
    public static ActionBase? Clone(ActionBase? action)
    {
        if (action == null) return null;

        try
        {
            var json = JsonSerializer.Serialize(action);
            return JsonSerializer.Deserialize<ActionBase>(json);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Aktion konnte nicht kopiert werden", ex);
            return null;
        }
    }
}
