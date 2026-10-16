using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HotKeyManager.Views;

/// <summary>Page for creating, editing and duplicating a hotkey.</summary>
public sealed partial class HotkeyEditorPage : Page
{
    /// <summary>Creates the page; the view model is created on navigation.</summary>
    public HotkeyEditorPage()
    {
        this.InitializeComponent();
    }

    /// <summary>The editor view model for the current navigation, null before the first navigation.</summary>
    public HotkeyEditorViewModel? ViewModel { get; private set; }

    /// <summary>x:Bind helper that negates a value.</summary>
    public bool Not(bool value) => !value;

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        this.ViewModel = new HotkeyEditorViewModel(e.Parameter as EditorNavArgs);
        this.ViewModel.CloseRequested += this.OnCloseRequested;
        this.Bindings.Update();
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (this.ViewModel is null)
        {
            return;
        }

        // Ends key capture, recording and the window countdown and resets IsCapturing;
        // otherwise hotkeys would stop firing after leaving the page.
        this.ViewModel.CancelAllCaptures();
        this.ViewModel.CloseRequested -= this.OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (this.Frame.CanGoBack)
        {
            this.Frame.GoBack();
        }
    }
}
