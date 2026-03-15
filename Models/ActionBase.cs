using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WebhookAction), "webhook")]
[JsonDerivedType(typeof(KeySequenceAction), "keysequence")]
[JsonDerivedType(typeof(ProcessAction), "process")]
[JsonDerivedType(typeof(BatchAction), "batch")]
[JsonDerivedType(typeof(SendTextAction), "sendtext")]
public abstract class ActionBase
{
    public abstract ActionType Type { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
}
