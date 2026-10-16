using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.Services.Execution;
using HotKeyManager.Services.Midi;

namespace HotKeyManager.Services;

/// <summary>
/// Runs actions. Decides where keys and text go (focused window, kernel driver, background window)
/// and hands the actual work to the classes in Services/Execution.
/// </summary>
public class ActionExecutor
{
    private readonly WebhookSender webhookSender = new();
    private readonly InterceptionService? interceptionService;

    /// <summary>Executor without kernel-mode injection.</summary>
    public ActionExecutor()
    {
    }

    /// <summary>Executor that can inject keys through the Interception driver.</summary>
    public ActionExecutor(InterceptionService interceptionService)
    {
        this.interceptionService = interceptionService;
    }

    /// <summary>Runs an action against the focused window.</summary>
    public Task ExecuteAsync(ActionBase action) => this.ExecuteAsync(action, WindowTargetMode.None, null, null);

    /// <summary>Runs an action with optional window targeting.</summary>
    /// <param name="targetProcessName">Target process, used with SendToBackground.</param>
    /// <param name="targetWindowTitle">Target title pattern, used with SendToBackground.</param>
    public async Task ExecuteAsync(
        ActionBase action,
        WindowTargetMode windowMode,
        string? targetProcessName,
        string? targetWindowTitle)
    {
        App.Current.LogService.Info($"Aktion ausfuehren: {action.GetType().Name}, WindowMode={windowMode}");
        var target = new Target(windowMode, targetProcessName, targetWindowTitle);
        switch (action)
        {
            case MacroAction macro:
                await this.ExecuteMacroAsync(macro, target);
                break;
            case KeySequenceAction keySequence:
                await this.PlayKeySequenceAsync(keySequence, target);
                break;
            case SendTextAction sendText:
                await TypeTextAsync(sendText, target);
                break;
            case DelayAction { DurationMs: > 0 } delay:
                await Task.Delay(delay.DurationMs);
                break;
            case WebhookAction webhook:
                await this.SendWebhookAsync(webhook);
                break;
            case ProcessAction process:
                Run(() => ProcessLauncher.Start(process), "Fehler beim Starten des Prozesses");
                break;
            case BatchAction batch:
                Run(() => ProcessLauncher.RunBatch(batch), "Batch-Befehl Fehler");
                break;
            case SystemAction system:
                SystemActionRunner.Run(system);
                break;
            case MidiSendAction midiSend:
                await MidiOutput.SendAsync(midiSend);
                break;
        }
    }

    /// <summary>Runs the macro blocks top to bottom with the optional delay between them.</summary>
    private async Task ExecuteMacroAsync(MacroAction macro, Target target)
    {
        for (var i = 0; i < macro.Steps.Count; i++)
        {
            await this.ExecuteAsync(macro.Steps[i], target.Mode, target.ProcessName, target.WindowTitle);

            var isLastStep = i == macro.Steps.Count - 1;
            if (macro.StepDelayMs > 0 && !isLastStep)
            {
                await Task.Delay(macro.StepDelayMs);
            }
        }
    }

    private async Task PlayKeySequenceAsync(KeySequenceAction action, Target target)
    {
        if (target.IsBackgroundWindow)
        {
            if (target.FindWindow() is { } windowHandle)
            {
                await KeySequencePlayer.PlayAsync(action, new WindowKeySink(windowHandle));
            }

            return;
        }

        IKeySink sink = action.UseKernelInjection && this.interceptionService is { CanSendKernel: true } interception
            ? new KernelKeySink(interception)
            : ForegroundKeySink.Instance;
        await KeySequencePlayer.PlayAsync(action, sink);
    }

    private static async Task TypeTextAsync(SendTextAction action, Target target)
    {
        if (string.IsNullOrEmpty(action.Text))
        {
            return;
        }

        try
        {
            if (!target.IsBackgroundWindow)
            {
                await TextTyper.TypeIntoForegroundAsync(action);
            }
            else if (target.FindWindow() is { } windowHandle)
            {
                await TextTyper.TypeIntoWindowAsync(action, windowHandle);
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("SendText-Fehler", ex);
        }
    }

    private async Task SendWebhookAsync(WebhookAction action)
    {
        try
        {
            await this.webhookSender.SendAsync(action);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Webhook-Fehler", ex);
        }
    }

    private static void Run(Action action, string errorMessage)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error(errorMessage, ex);
        }
    }

    /// <summary>Where keys and text of one execution go.</summary>
    private sealed record Target(WindowTargetMode Mode, string? ProcessName, string? WindowTitle)
    {
        public bool IsBackgroundWindow => this.Mode == WindowTargetMode.SendToBackground;

        /// <summary>The target window, or null (logged) when it does not exist right now.</summary>
        public IntPtr? FindWindow()
        {
            var handle = WindowHelper.FindWindowByProcessAndTitle(this.ProcessName, this.WindowTitle);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            App.Current.LogService.Warning(
                $"Zielfenster nicht gefunden: Process={this.ProcessName}, Title={this.WindowTitle}");
            return null;
        }
    }
}
