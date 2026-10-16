namespace HotKeyManager.Models;

/// <summary>Sends a sequence of key strokes.</summary>
public class KeySequenceAction : ActionBase
{
    public override ActionType Type => ActionType.KeySequence;

    public override string DisplayName => "Tastensequenz";

    public override string Description => $"{this.Keys.Count} Tasten";

    public List<KeyStroke> Keys { get; set; } = new();

    /// <summary>
    /// Injects the keys through the kernel-mode driver, so they carry no LLKHF_INJECTED flag.
    /// Needed for apps that filter synthetic input (e.g. via the Python keyboard library).
    /// </summary>
    public bool UseKernelInjection { get; set; }
}
