using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>Base class of every action a hotkey can run; serialized polymorphically via "$type".</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WebhookAction), "webhook")]
[JsonDerivedType(typeof(KeySequenceAction), "keysequence")]
[JsonDerivedType(typeof(ProcessAction), "process")]
[JsonDerivedType(typeof(BatchAction), "batch")]
[JsonDerivedType(typeof(SendTextAction), "sendtext")]
[JsonDerivedType(typeof(DelayAction), "delay")]
[JsonDerivedType(typeof(MacroAction), "macro")]
[JsonDerivedType(typeof(SystemAction), "system")]
[JsonDerivedType(typeof(MidiSendAction), "midisend")]
public abstract class ActionBase
{
    /// <summary>The kind of action, used to pick the editor and executor.</summary>
    [JsonIgnore]
    public abstract ActionType Type { get; }

    /// <summary>Name of the action kind as shown in the UI.</summary>
    [JsonIgnore]
    public abstract string DisplayName { get; }

    /// <summary>Short summary of the configured action for list rows.</summary>
    [JsonIgnore]
    public abstract string Description { get; }
}
