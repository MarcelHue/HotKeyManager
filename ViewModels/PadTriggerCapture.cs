using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;

namespace HotKeyManager.ViewModels;

/// <summary>DisplayPad trigger of a hotkey: PadDeck reports the next key pressed on the pad.</summary>
public partial class PadTriggerCapture : ObservableObject
{
    private const string NotSetText = "Nicht gesetzt";
    private const string IdleButtonText = "Taste lernen";

    private static readonly TimeSpan LearnTimeout = TimeSpan.FromSeconds(20);

    private readonly HotkeyManagerService hotkeyService;
    private readonly Guid? ownHotkeyId;
    private readonly Action<string> showError;
    private CancellationTokenSource? learnCancellation;

    [ObservableProperty]
    private string displayText = NotSetText;

    [ObservableProperty]
    private string buttonText = IdleButtonText;

    [ObservableProperty]
    private bool isButtonEnabled = true;

    [ObservableProperty]
    private bool hasTrigger;

    /// <summary>Hint below the trigger, e.g. that a PadDeck action gets switched off.</summary>
    [ObservableProperty]
    private string hint = string.Empty;

    /// <summary>Creates the capture for one editor page.</summary>
    /// <param name="ownHotkeyId">The hotkey being edited; its own pad key does not count as taken.</param>
    /// <param name="showError">Shows a message in the page's error bar.</param>
    public PadTriggerCapture(HotkeyManagerService hotkeyService, Guid? ownHotkeyId, Action<string> showError)
    {
        this.hotkeyService = hotkeyService;
        this.ownHotkeyId = ownHotkeyId;
        this.showError = showError;
    }

    /// <summary>PadDeck is installed or connected; otherwise the pad section stays hidden.</summary>
    public bool IsAvailable => App.Current.PadDeckBridge.IsAvailable;

    public PadTrigger? Trigger { get; private set; }

    /// <summary>Shows an existing trigger (or none).</summary>
    public void Set(PadTrigger? trigger)
    {
        this.Trigger = trigger;
        this.UpdateDisplay();
    }

    /// <summary>Stops waiting for a pad key; a later learn replaces the request on the PadDeck side.</summary>
    public void StopLearn() => this.learnCancellation?.Cancel();

    [RelayCommand]
    private async Task StartLearnAsync()
    {
        var bridge = App.Current.PadDeckBridge;
        if (!bridge.IsConnected)
        {
            this.showError("PadDeck läuft nicht. Starte PadDeck und versuche es erneut.");
            return;
        }

        using var cancellation = new CancellationTokenSource();
        this.learnCancellation = cancellation;
        this.DisplayText = "Drücke jetzt eine Taste am DisplayPad …";
        this.ButtonText = "Warte …";
        this.IsButtonEnabled = false;
        this.Hint = string.Empty;
        try
        {
            var key = await bridge.LearnKeyAsync(LearnTimeout).WaitAsync(cancellation.Token);
            this.Apply(key);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cleared or page left while waiting: keep whatever is set now.
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Pad-Taste lernen fehlgeschlagen", ex);
            this.Hint = "PadDeck hat nicht geantwortet.";
        }
        finally
        {
            this.learnCancellation = null;
            this.ButtonText = IdleButtonText;
            this.IsButtonEnabled = true;
            this.UpdateDisplay();
        }
    }

    private void Apply(PadKeyInfo? key)
    {
        if (key is null)
        {
            this.Hint = "Keine Taste gedrückt — versuche es noch einmal.";
            return;
        }

        var other = this.hotkeyService.Hotkeys.FirstOrDefault(h =>
            h.Id != this.ownHotkeyId && h.PadTrigger?.Matches(key.ProfileId, key.PageId, key.Slot) == true);
        if (other is not null)
        {
            this.Hint = $"Diese Taste löst schon „{other.Name}“ aus.";
            return;
        }

        this.Trigger = new PadTrigger
        {
            ProfileId = key.ProfileId,
            PageId = key.PageId,
            Slot = key.Slot,
            ProfileName = key.ProfileName,
            PageName = key.PageName,
        };
        if (key.HasPadDeckAction)
        {
            this.Hint = "Die bisherige PadDeck-Aktion dieser Taste wird abgeschaltet, "
                + "solange HotKeyManager sie belegt.";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        this.StopLearn();
        this.Hint = string.Empty;
        this.Set(null);
    }

    private void UpdateDisplay()
    {
        this.HasTrigger = this.Trigger is not null;
        this.DisplayText = this.Trigger?.DisplayText ?? NotSetText;
    }
}
