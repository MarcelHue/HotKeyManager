using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>Window targeting of a hotkey; the target is picked by focusing it during a short countdown.</summary>
public partial class WindowTargetPicker : ObservableObject
{
    private const int CaptureSeconds = 3;

    private static readonly string IdleButtonText = $"Fenster auswählen ({CaptureSeconds}s)";

    private readonly DispatcherQueue dispatcherQueue;
    private DispatcherQueueTimer? captureTimer;
    private int countdown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTargetVisible))]
    private int modeIndex;

    [ObservableProperty]
    private string processName = string.Empty;

    [ObservableProperty]
    private string windowTitle = string.Empty;

    [ObservableProperty]
    private bool isCapturing;

    [ObservableProperty]
    private string buttonText = IdleButtonText;

    /// <summary>Creates the picker for one editor page.</summary>
    public WindowTargetPicker(DispatcherQueue dispatcherQueue)
    {
        this.dispatcherQueue = dispatcherQueue;
    }

    public bool IsTargetVisible => this.ModeIndex != 0;

    public WindowTargetMode Mode => (WindowTargetMode)this.ModeIndex;

    public string? ProcessNameOrNull => TrimToNull(this.ProcessName);

    public string? WindowTitleOrNull => TrimToNull(this.WindowTitle);

    /// <summary>Shows the targeting of an existing hotkey.</summary>
    public void Set(WindowTargetMode mode, string? process, string? title)
    {
        this.ModeIndex = (int)mode;
        this.ProcessName = process ?? string.Empty;
        this.WindowTitle = title ?? string.Empty;
    }

    /// <summary>Stops a running countdown without taking a window.</summary>
    public void StopCapture()
    {
        this.captureTimer?.Stop();
        this.captureTimer = null;

        this.IsCapturing = false;
        this.ButtonText = IdleButtonText;
    }

    private static string? TrimToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [RelayCommand]
    private void Capture()
    {
        if (this.IsCapturing)
        {
            return;
        }

        this.IsCapturing = true;
        this.countdown = CaptureSeconds;
        this.ButtonText = $"Erfasse in {this.countdown}s...";

        this.captureTimer = this.dispatcherQueue.CreateTimer();
        this.captureTimer.Interval = TimeSpan.FromSeconds(1);
        this.captureTimer.Tick += (_, _) => this.OnTick();
        this.captureTimer.Start();
    }

    private void OnTick()
    {
        this.countdown--;
        if (this.countdown > 0)
        {
            this.ButtonText = $"Erfasse in {this.countdown}s...";
            return;
        }

        if (WindowHelper.GetActiveWindowInfo() is { } windowInfo)
        {
            this.ProcessName = windowInfo.ProcessName;
            this.WindowTitle = windowInfo.Title;
        }

        this.StopCapture();
    }
}
