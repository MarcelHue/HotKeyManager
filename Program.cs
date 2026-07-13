using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace HotKeyManager;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Muss als Allererstes laufen: verarbeitet Velopack-Install/Update/Uninstall-Hooks
        // und beendet den Prozess ggf. sofort wieder (z.B. waehrend eines Updates).
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
