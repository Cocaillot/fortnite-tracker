using System.IO;
using Microsoft.Win32;

namespace FortniteTracker.Desktop;

/// <summary>
/// "Open with Fortnite": the app starts at Windows sign-in with <see cref="BackgroundArg"/>, stays in
/// the tray without loading its window, and opens the window once Fortnite is running.
/// </summary>
public static class AutoStart
{
    public const string BackgroundArg = "--background";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FortniteTracker";

    // Velopack keeps the installed app in ...\FortniteTracker\current, whatever the version, so the
    // path stays valid across updates. Development builds never register themselves.
    private static bool IsInstalled =>
        Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) == "current";

    public static void Apply(bool enabled)
    {
        if (!IsInstalled && enabled) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {BackgroundArg}");
            else if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Blocked by policy: the app still works, it just won't open by itself.
        }
    }
}
