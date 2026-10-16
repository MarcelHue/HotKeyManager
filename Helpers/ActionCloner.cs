using System.Text.Json;
using HotKeyManager.Models;

namespace HotKeyManager.Helpers;

/// <summary>
/// Deep-copies actions through a JSON round trip. This reuses the $type polymorphism,
/// so nested macro steps are copied correctly too.
/// </summary>
public static class ActionCloner
{
    /// <summary>Returns a deep copy of the action, or null if it is null or cannot be copied.</summary>
    public static ActionBase? Clone(ActionBase? action)
    {
        if (action is null)
        {
            return null;
        }

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
