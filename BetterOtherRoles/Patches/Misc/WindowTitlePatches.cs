using System.Diagnostics;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Window title of the game: "[BetterOtherRoles-v0.0.1a] : Among Us" (version with its suffix shortened: alpha → a,
///     beta → b). Set when the mod is loaded, and again on the main menu in case the window did not exist yet (Windows only).
/// </summary>
[HarmonyPatch]
public static class WindowTitlePatches
{
    /// <summary>
    ///     Gets the window title.
    /// </summary>
    public static string Title => $"[BetterOtherRoles-v{ShortVersion(BetterOtherRolesPlugin.Version)}] : Among Us";

    /// <summary>
    ///     Gives its title to the game window.
    /// </summary>
    public static void Apply()
    {
        if (!OperatingSystem.IsWindows()) return;

        var window = FindGameWindow();
        if (window == IntPtr.Zero || !SetWindowText(window, Title))
            Warning("Window title: game window not found.");
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPostfix]
    public static void MainMenuStartPostfix()
    {
        Apply();
    }

    // "0.0.1-alpha" → "0.0.1a", "1.2.0-beta" → "1.2.0b"; other suffixes are kept.
    private static string ShortVersion(string version)
    {
        return version.Replace("-alpha", "a", StringComparison.OrdinalIgnoreCase).Replace("-beta", "b", StringComparison.OrdinalIgnoreCase);
    }

    // The visible main window of this process (Unity: "UnityWndClass").
    private static IntPtr FindGameWindow()
    {
        var processId = (uint)Environment.ProcessId;
        var found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (GetWindowThreadProcessId(window, out var windowProcessId) == 0 || windowProcessId != processId || !IsWindowVisible(window))
                return true;

            var className = new char[64];
            var length = GetClassName(window, className, className.Length);
            if (new string(className, 0, Math.Max(length, 0)) != "UnityWndClass") return true;

            found = window;
            return false;
        }, IntPtr.Zero);

        return found != IntPtr.Zero ? found : Process.GetCurrentProcess().MainWindowHandle;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, [Out] char[] className, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr window, string text);
}
