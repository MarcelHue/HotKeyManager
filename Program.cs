using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace HotKeyManager;

/// <summary>Custom entry point so Velopack runs before XAML starts.</summary>
public static class Program
{
    [STAThread]
    private static void Main()
    {
        // Must run first: handles Velopack install/update/uninstall hooks and may end the
        // process right away (e.g. during an update).
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(static p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
