using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WebhookAction), "webhook")]
[JsonDerivedType(typeof(KeySequenceAction), "keysequence")]
[JsonDerivedType(typeof(ProcessAction), "process")]
[JsonDerivedType(typeof(BatchAction), "batch")]
[JsonDerivedType(typeof(SendTextAction), "sendtext")]
[JsonDerivedType(typeof(DelayAction), "delay")]
[JsonDerivedType(typeof(MacroAction), "macro")]
public abstract class ActionBase
{
    [JsonIgnore]
    public abstract ActionType Type { get; }
    [JsonIgnore]
    public abstract string DisplayName { get; }
    [JsonIgnore]
    public abstract string Description { get; }
}
