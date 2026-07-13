using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HotKeyManager.Views;

public sealed partial class HotkeyEditorPage : Page
{
    public HotkeyEditorViewModel? ViewModel { get; private set; }

    public HotkeyEditorPage()
    {
        this.InitializeComponent();
    }

    public bool Not(bool value) => !value;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel = new HotkeyEditorViewModel(e.Parameter as EditorNavArgs);
        ViewModel.CloseRequested += OnCloseRequested;
        Bindings.Update();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (ViewModel != null)
        {
            // Stoppt Key-Capture, Aufnahme und Fenster-Countdown, setzt IsCapturing zurueck —
            // sonst wuerden Hotkeys nach dem Wegnavigieren nicht mehr feuern.
            ViewModel.CancelAllCaptures();
            ViewModel.CloseRequested -= OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }
}
