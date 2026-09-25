using System.Globalization;
using System.Threading;
using Velopack;

namespace FortniteTracker.Desktop;

public static class Program
{
    private const string MutexName = @"Local\FortniteTracker.SingleInstance";
    private const string ShowEventName = @"Local\FortniteTracker.Show";

    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: during install/update/uninstall, Velopack launches the exe with hook
        // arguments and this call handles them and exits.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => AutoStart.Apply(false))
            .Run();

        // Native text formats numbers like the web UI ("6.80") whatever the Windows region; French
        // text uses Loc.Culture explicitly.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        // After a restore the app restarts itself: wait for the old instance to exit.
        if (!isFirstInstance && args.Contains(App.RestartArg))
        {
            try { isFirstInstance = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
            catch (AbandonedMutexException) { isFirstInstance = true; }
        }
        if (!isFirstInstance)
        {
            // Already running (probably hidden in the tray): ask it to show itself instead.
            showEvent.Set();
            return;
        }

        var app = new App(showEvent);
        app.InitializeComponent();
        app.Run();
    }
}
