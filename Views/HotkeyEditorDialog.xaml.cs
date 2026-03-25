using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HotKeyManager.Models;
using HotKeyManager.Helpers;
using HotKeyManager.Services;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using WinRT.Interop;
using DispatcherTimer = Microsoft.UI.Xaml.DispatcherTimer;


namespace HotKeyManager.Views;

public sealed partial class HotkeyEditorDialog : ContentDialog
{
    public HotkeyDefinition? Hotkey { get; private set; }

    private int _capturedKeyCode;
    private ModifierKeys _capturedModifiers;
    private readonly ObservableCollection<KeyStroke> _keyStrokes = new();
    private readonly bool _isEditing;

    private bool _isCapturing;
    private bool _isCapturingKeyStroke;
    private bool _isAdvancedMode;
    private bool _isRecording;
    private bool _useKernelInjection;
    private bool _isCapturingWindow;
    private WindowTargetMode _windowMode = WindowTargetMode.None;
    private KeyboardHookService? _keyboardHook;
    private DispatcherTimer? _windowCaptureTimer;
    private int _windowCaptureCountdown;
    private FrameworkElement? _mainWindowContent;

    public HotkeyEditorDialog()
    {
        this.InitializeComponent();
        _isEditing = false;
        Title = "Neuer Hotkey";
        ActionTypeComboBox.SelectedIndex = 0;
        KeyStrokesListView.ItemsSource = _keyStrokes;

        _keyStrokes.CollectionChanged += (s, e) => UpdateEmptyKeyStrokesHint();
        KeyPickerListView.ItemsSource = KeyHelper.GetAllKeys();

        if (App.Current.MainWindow is MainWindow mainWindow)
        {
            ApplyDialogSize(mainWindow.CurrentSize);

            _mainWindowContent = mainWindow.Content as FrameworkElement;
            if (_mainWindowContent != null)
                _mainWindowContent.SizeChanged += OnMainWindowSizeChanged;
        }
    }

    public HotkeyEditorDialog(HotkeyDefinition hotkey) : this()
    {
        _isEditing = true;
        Title = "Hotkey bearbeiten";
        Hotkey = hotkey;
        LoadHotkey(hotkey);
    }

    private void LoadHotkey(HotkeyDefinition hotkey)
    {
        NameTextBox.Text = hotkey.Name;
        _capturedKeyCode = hotkey.VirtualKeyCode;
        _capturedModifiers = hotkey.Modifiers;
        UpdateHotkeyDisplay();
        
        // Window Targeting laden
        _windowMode = hotkey.WindowMode;
        switch (hotkey.WindowMode)
        {
            case WindowTargetMode.OnlyWhenActive:
                WindowModeOnlyActive.IsChecked = true;
                break;
            case WindowTargetMode.SendToBackground:
                WindowModeSendToBackground.IsChecked = true;
                break;
            default:
                WindowModeNone.IsChecked = true;
                break;
        }
        TargetProcessNameTextBox.Text = hotkey.TargetProcessName ?? string.Empty;
        TargetWindowTitleTextBox.Text = hotkey.TargetWindowTitle ?? string.Empty;
        UpdateWindowTargetConfigVisibility();

        switch (hotkey.Action)
        {
            case WebhookAction webhook:
                ActionTypeComboBox.SelectedIndex = 0;
                WebhookUrlTextBox.Text = webhook.Url;
                HttpMethodComboBox.SelectedIndex = (int)webhook.Method;
                SetComboBoxByContent(ContentTypeComboBox, webhook.ContentType);
                WebhookBodyTextBox.Text = webhook.Body;
                break;

            case KeySequenceAction keySeq:
                ActionTypeComboBox.SelectedIndex = 1;
                // Check if advanced mode was used (any KeyDown/KeyUp events)
                var hasAdvancedKeys = keySeq.Keys.Any(k => k.EventType != KeyEventType.KeyPress);
                if (hasAdvancedKeys)
                {
                    _isAdvancedMode = true;
                    AdvancedModeCheckBox.IsChecked = true;
                }
                _useKernelInjection = keySeq.UseKernelInjection;
                KernelInjectionCheckBox.IsChecked = keySeq.UseKernelInjection;
                UpdateKernelInjectionWarning();
                foreach (var stroke in keySeq.Keys)
                {
                    _keyStrokes.Add(stroke);
                }
                break;

            case ProcessAction process:
                ActionTypeComboBox.SelectedIndex = 2;
                ProcessPathTextBox.Text = process.FilePath;
                ProcessArgsTextBox.Text = process.Arguments;
                ProcessWorkDirTextBox.Text = process.WorkingDirectory;
                ProcessRunAsAdminCheckBox.IsChecked = process.RunAsAdmin;
                ProcessHiddenCheckBox.IsChecked = process.Hidden;
                break;

            case BatchAction batch:
                ActionTypeComboBox.SelectedIndex = 3;
                BatchCommandTextBox.Text = batch.Command;
                BatchWorkDirTextBox.Text = batch.WorkingDirectory;
                BatchHiddenCheckBox.IsChecked = batch.RunHidden;
                BatchWaitCheckBox.IsChecked = batch.WaitForExit;
                break;
            
            case SendTextAction sendText:
                ActionTypeComboBox.SelectedIndex = 4;
                SendTextTextBox.Text = sendText.Text;
                break;
        }
    }

    private void SetComboBoxByContent(ComboBox comboBox, string content)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && item.Content?.ToString() == content)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        // Start inline capture mode (no second dialog - WinUI only allows one ContentDialog at a time)
        StartInlineCapture();
    }

    private void StartInlineCapture()
    {
        if (_isCapturing) return;

        _isCapturing = true;
        _keyboardHook = App.Current.KeyboardHookService;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressedForCapture;

        // Update UI to show capture mode
        HotkeyDisplayText.Text = "Drücke eine Taste...";
        CaptureKeyButton.Content = "Erfassen...";
        CaptureKeyButton.IsEnabled = false;
    }

    private void StopInlineCapture()
    {
        if (!_isCapturing) return;

        _isCapturing = false;
        if (_keyboardHook != null)
        {
            _keyboardHook.KeyPressed -= OnKeyPressedForCapture;
            _keyboardHook.IsCapturing = false;
        }

        CaptureKeyButton.Content = "Taste erfassen";
        CaptureKeyButton.IsEnabled = true;
    }

    private void OnKeyPressedForCapture(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            _capturedKeyCode = e.VirtualKeyCode;
            _capturedModifiers = e.Modifiers;
            UpdateHotkeyDisplay();
            StopInlineCapture();
        });
    }

    private void UpdateHotkeyDisplay()
    {
        if (_capturedKeyCode == 0)
        {
            HotkeyDisplayText.Text = "Nicht gesetzt";
            return;
        }

        var parts = new List<string>();

        if (_capturedModifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (_capturedModifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (_capturedModifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (_capturedModifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(KeyHelper.GetKeyName(_capturedKeyCode));

        HotkeyDisplayText.Text = string.Join(" + ", parts);
    }

    private void KernelInjection_Changed(object sender, RoutedEventArgs e)
    {
        _useKernelInjection = KernelInjectionCheckBox.IsChecked ?? false;
        UpdateKernelInjectionWarning();
    }

    private void UpdateKernelInjectionWarning()
    {
        var driverReady = App.Current.InterceptionService.IsRunning || InterceptionService.IsDriverActive();
        KernelInjectionWarning.IsOpen = _useKernelInjection && !driverReady;
    }

    private void ActionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        WebhookConfig.Visibility = Visibility.Collapsed;
        KeySequenceConfig.Visibility = Visibility.Collapsed;
        ProcessConfig.Visibility = Visibility.Collapsed;
        BatchConfig.Visibility = Visibility.Collapsed;
        SendTextConfig.Visibility = Visibility.Collapsed;

        switch (ActionTypeComboBox.SelectedIndex)
        {
            case 0:
                WebhookConfig.Visibility = Visibility.Visible;
                break;
            case 1:
                KeySequenceConfig.Visibility = Visibility.Visible;
                break;
            case 2:
                ProcessConfig.Visibility = Visibility.Visible;
                break;
            case 3:
                BatchConfig.Visibility = Visibility.Visible;
                break;
            case 4:
                SendTextConfig.Visibility = Visibility.Visible;
                break;
        }
    }

    #region Window Targeting
    
    private void WindowMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowModeNone.IsChecked == true)
            _windowMode = WindowTargetMode.None;
        else if (WindowModeOnlyActive.IsChecked == true)
            _windowMode = WindowTargetMode.OnlyWhenActive;
        else if (WindowModeSendToBackground.IsChecked == true)
            _windowMode = WindowTargetMode.SendToBackground;
        
        UpdateWindowTargetConfigVisibility();
    }
    
    private void UpdateWindowTargetConfigVisibility()
    {
        WindowTargetConfig.Visibility = _windowMode != WindowTargetMode.None 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }
    
    private void CaptureWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingWindow) return;
        
        StartWindowCapture();
    }
    
    private void StartWindowCapture()
    {
        _isCapturingWindow = true;
        _windowCaptureCountdown = 3;
        
        WindowCaptureInfoBar.IsOpen = true;
        CaptureWindowButton.IsEnabled = false;
        UpdateWindowCaptureButtonText();
        
        _windowCaptureTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _windowCaptureTimer.Tick += WindowCaptureTimer_Tick;
        _windowCaptureTimer.Start();
    }
    
    private void WindowCaptureTimer_Tick(object? sender, object e)
    {
        _windowCaptureCountdown--;
        
        if (_windowCaptureCountdown <= 0)
        {
            // Zeit abgelaufen - Fenster erfassen
            CaptureCurrentWindow();
            StopWindowCapture();
        }
        else
        {
            UpdateWindowCaptureButtonText();
        }
    }
    
    private void CaptureCurrentWindow()
    {
        var windowInfo = WindowHelper.GetActiveWindowInfo();
        if (windowInfo != null)
        {
            TargetProcessNameTextBox.Text = windowInfo.ProcessName;
            TargetWindowTitleTextBox.Text = windowInfo.Title;
        }
    }
    
    private void StopWindowCapture()
    {
        _isCapturingWindow = false;
        
        _windowCaptureTimer?.Stop();
        _windowCaptureTimer = null;
        
        WindowCaptureInfoBar.IsOpen = false;
        CaptureWindowButton.IsEnabled = true;
        CaptureWindowButtonText.Text = "Fenster auswählen (3s)";
    }
    
    private void UpdateWindowCaptureButtonText()
    {
        CaptureWindowButtonText.Text = $"Erfasse in {_windowCaptureCountdown}s...";
    }
    
    #endregion

    private void AddKeyStroke_Click(object sender, RoutedEventArgs e)
    {
        StartKeyStrokeCapture();
    }

    private void StartKeyStrokeCapture()
    {
        if (_isCapturingKeyStroke || _isCapturing || _isRecording) return;

        _isCapturingKeyStroke = true;
        _keyboardHook = App.Current.KeyboardHookService;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressedForKeyStroke;

        // Update button content
        if (AddKeyStrokeButton.Content is StackPanel panel && panel.Children.Count > 1
            && panel.Children[1] is TextBlock textBlock)
        {
            textBlock.Text = "Drücke eine Taste...";
        }
        AddKeyStrokeButton.IsEnabled = false;
    }

    private void StopKeyStrokeCapture()
    {
        if (!_isCapturingKeyStroke) return;

        _isCapturingKeyStroke = false;
        if (_keyboardHook != null)
        {
            _keyboardHook.KeyPressed -= OnKeyPressedForKeyStroke;
            _keyboardHook.IsCapturing = false;
        }

        // Restore button content
        if (AddKeyStrokeButton.Content is StackPanel panel && panel.Children.Count > 1
            && panel.Children[1] is TextBlock textBlock)
        {
            textBlock.Text = "Taste erfassen";
        }
        AddKeyStrokeButton.IsEnabled = true;
    }

    private void OnKeyPressedForKeyStroke(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            var keyStroke = new KeyStroke
            {
                VirtualKeyCode = e.VirtualKeyCode,
                Modifiers = e.Modifiers,
                DelayAfterMs = 50,
                DisplayText = e.KeyDisplayText
            };
            _keyStrokes.Add(keyStroke);
            StopKeyStrokeCapture();
        });
    }

    private void KeySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        var query = sender.Text?.Trim() ?? string.Empty;
        var allKeys = KeyHelper.GetAllKeys();

        if (string.IsNullOrEmpty(query))
        {
            KeyPickerListView.ItemsSource = allKeys;
            sender.ItemsSource = null;
        }
        else
        {
            var filtered = allKeys
                .Where(k => k.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            sender.ItemsSource = filtered.Select(k => k.Name).ToList();
            KeyPickerListView.ItemsSource = filtered;
        }
    }

    private void KeySearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string keyName)
        {
            var entry = KeyHelper.GetAllKeys().FirstOrDefault(k => k.Name == keyName);
            if (entry != null)
            {
                AddManualKeyStroke(entry);
                sender.Text = string.Empty;
                ManualKeyFlyout.Hide();
            }
        }
    }

    private void KeyPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyPickerListView.SelectedItem is KeyHelper.VirtualKeyEntry entry)
        {
            AddManualKeyStroke(entry);
            KeyPickerListView.SelectedItem = null;
            KeySearchBox.Text = string.Empty;
            ManualKeyFlyout.Hide();
        }
    }

    private void AddManualKeyStroke(KeyHelper.VirtualKeyEntry entry)
    {
        var keyStroke = new KeyStroke
        {
            VirtualKeyCode = entry.VirtualKeyCode,
            Modifiers = ModifierKeys.None,
            DelayAfterMs = 50,
            DisplayText = entry.Name,
            EventType = _isAdvancedMode ? KeyEventType.KeyDown : KeyEventType.KeyPress
        };
        _keyStrokes.Add(keyStroke);

        if (_isAdvancedMode)
        {
            _keyStrokes.Add(new KeyStroke
            {
                VirtualKeyCode = entry.VirtualKeyCode,
                Modifiers = ModifierKeys.None,
                DelayAfterMs = 10,
                DisplayText = entry.Name,
                EventType = KeyEventType.KeyUp
            });
        }
    }

    private void RemoveKeyStroke_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyStroke stroke)
        {
            _keyStrokes.Remove(stroke);
        }
    }

    #region Advanced Mode

    private void AdvancedMode_Changed(object sender, RoutedEventArgs e)
    {
        _isAdvancedMode = AdvancedModeCheckBox.IsChecked ?? false;

        AddKeyStrokeButton.Visibility = _isAdvancedMode ? Visibility.Collapsed : Visibility.Visible;
        ManualKeyButton.Visibility = _isAdvancedMode ? Visibility.Collapsed : Visibility.Visible;
        RecordButton.Visibility = _isAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        AdvancedModeInfoBar.IsOpen = _isAdvancedMode;

        // Stop any active recording when switching modes
        if (!_isAdvancedMode && _isRecording)
        {
            StopRecording();
        }
    }

    private void ToggleRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (_isRecording || _isCapturing || _isCapturingKeyStroke) return;

        _isRecording = true;
        _keyboardHook = App.Current.KeyboardHookService;
        _keyboardHook.IsCapturing = true;
        _keyboardHook.CaptureModifierKeys = true; // Im Advanced Mode auch Modifier-Tasten aufzeichnen
        _keyboardHook.KeyPressed += OnRecordKeyDown;
        _keyboardHook.KeyReleased += OnRecordKeyUp;

        // Update UI
        RecordButtonText.Text = "Aufnahme stoppen";
        RecordIcon.Glyph = "\uE71A"; // Stop icon
        RecordButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.IndianRed);
    }

    private void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;
        if (_keyboardHook != null)
        {
            _keyboardHook.KeyPressed -= OnRecordKeyDown;
            _keyboardHook.KeyReleased -= OnRecordKeyUp;
            _keyboardHook.IsCapturing = false;
            _keyboardHook.CaptureModifierKeys = false; // Zurücksetzen
        }

        // Update UI
        RecordButtonText.Text = "Aufnahme starten";
        RecordIcon.Glyph = "\uE7C8"; // Record icon
        RecordButton.Background = null;
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            var keyStroke = new KeyStroke
            {
                VirtualKeyCode = e.VirtualKeyCode,
                Modifiers = ModifierKeys.None, // Im Advanced Mode keine Modifiers, da separat aufgezeichnet
                DelayAfterMs = 10,
                DisplayText = KeyHelper.GetKeyName(e.VirtualKeyCode),
                EventType = KeyEventType.KeyDown
            };
            _keyStrokes.Add(keyStroke);
        });
    }

    private void OnRecordKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            var keyStroke = new KeyStroke
            {
                VirtualKeyCode = e.VirtualKeyCode,
                Modifiers = ModifierKeys.None,
                DelayAfterMs = 10,
                DisplayText = KeyHelper.GetKeyName(e.VirtualKeyCode),
                EventType = KeyEventType.KeyUp
            };
            _keyStrokes.Add(keyStroke);
        });
    }

    private void UpdateEmptyKeyStrokesHint()
    {
        EmptyKeyStrokesHint.Visibility = _keyStrokes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ProcessPathTextBox.Text = file.Path;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            args.Cancel = true;
            ShowError("Bitte gib einen Namen ein.");
            return;
        }

        if (_capturedKeyCode == 0)
        {
            args.Cancel = true;
            ShowError("Bitte erfasse eine Tastenkombination.");
            return;
        }

        // Build hotkey
        Hotkey = new HotkeyDefinition
        {
            Id = _isEditing && Hotkey != null ? Hotkey.Id : Guid.NewGuid(),
            Name = NameTextBox.Text.Trim(),
            VirtualKeyCode = _capturedKeyCode,
            Modifiers = _capturedModifiers,
            IsEnabled = true,
            WindowMode = _windowMode,
            TargetProcessName = string.IsNullOrWhiteSpace(TargetProcessNameTextBox.Text) ? null : TargetProcessNameTextBox.Text.Trim(),
            TargetWindowTitle = string.IsNullOrWhiteSpace(TargetWindowTitleTextBox.Text) ? null : TargetWindowTitleTextBox.Text.Trim(),
            Action = BuildAction()
        };

        if (Hotkey.Action == null)
        {
            args.Cancel = true;
            ShowError("Bitte konfiguriere die Aktion.");
            return;
        }
    }

    private ActionBase? BuildAction()
    {
        return ActionTypeComboBox.SelectedIndex switch
        {
            0 => BuildWebhookAction(),
            1 => BuildKeySequenceAction(),
            2 => BuildProcessAction(),
            3 => BuildBatchAction(),
            4 => BuildSendTextAction(),
            _ => null
        };
    }

    private WebhookAction? BuildWebhookAction()
    {
        if (string.IsNullOrWhiteSpace(WebhookUrlTextBox.Text))
            return null;

        return new WebhookAction
        {
            Url = WebhookUrlTextBox.Text.Trim(),
            Method = (HttpMethodType)HttpMethodComboBox.SelectedIndex,
            ContentType = (ContentTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "application/json",
            Body = WebhookBodyTextBox.Text
        };
    }

    private KeySequenceAction? BuildKeySequenceAction()
    {
        if (_keyStrokes.Count == 0)
            return null;

        return new KeySequenceAction
        {
            Keys = _keyStrokes.ToList(),
            UseKernelInjection = _useKernelInjection
        };
    }

    private ProcessAction? BuildProcessAction()
    {
        if (string.IsNullOrWhiteSpace(ProcessPathTextBox.Text))
            return null;

        return new ProcessAction
        {
            FilePath = ProcessPathTextBox.Text.Trim(),
            Arguments = ProcessArgsTextBox.Text,
            WorkingDirectory = ProcessWorkDirTextBox.Text,
            RunAsAdmin = ProcessRunAsAdminCheckBox.IsChecked ?? false,
            Hidden = ProcessHiddenCheckBox.IsChecked ?? false
        };
    }

    private BatchAction? BuildBatchAction()
    {
        if (string.IsNullOrWhiteSpace(BatchCommandTextBox.Text))
            return null;

        return new BatchAction
        {
            Command = BatchCommandTextBox.Text,
            WorkingDirectory = BatchWorkDirTextBox.Text,
            RunHidden = BatchHiddenCheckBox.IsChecked ?? true,
            WaitForExit = BatchWaitCheckBox.IsChecked ?? false
        };
    }
    
    private SendTextAction? BuildSendTextAction()
    {
        if (string.IsNullOrWhiteSpace(SendTextTextBox.Text))
            return null;

        return new SendTextAction
        {
            Text = SendTextTextBox.Text
        };
    }

    private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        StopInlineCapture();
        StopKeyStrokeCapture();
        StopRecording();
        StopWindowCapture();

        if (_mainWindowContent != null)
            _mainWindowContent.SizeChanged -= OnMainWindowSizeChanged;
    }

    private void OnMainWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (App.Current.MainWindow is MainWindow mainWindow)
            ApplyDialogSize(mainWindow.CurrentSize);
    }

    private void ApplyDialogSize(Windows.Graphics.SizeInt32 windowSize)
    {
        var dialogWidth = windowSize.Width * 0.9;
        var dialogHeight = windowSize.Height * 0.9;

        this.Resources["ContentDialogMaxWidth"] = dialogWidth;
        this.Resources["ContentDialogMaxHeight"] = dialogHeight;
        this.Resources["ContentDialogMinWidth"] = (double)GlobalConst.DEFAULT_DIALOG_WIDTH;
        this.Resources["ContentDialogMinHeight"] = (double)GlobalConst.DEFAULT_DIALOG_HEIGHT;

        RootPanel.Width = dialogWidth;
        MainScrollViewer.MaxHeight = dialogHeight;
    }

    private void ShowError(string message)
    {
        // Note: Can't show another ContentDialog while this one is open
        // Use InfoBar for errors
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }
}
