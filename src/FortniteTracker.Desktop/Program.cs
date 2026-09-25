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
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
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
