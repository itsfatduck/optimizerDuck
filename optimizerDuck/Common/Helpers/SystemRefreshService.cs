using System.Diagnostics;
using System.Runtime.InteropServices;

namespace optimizerDuck.Common.Helpers;

/// <summary>
/// Provides targeted Windows API-based refresh mechanisms instead of restarting Explorer.
/// Uses WM_SETTINGCHANGE broadcasts and SHChangeNotify for shell/explorer settings.
/// </summary>
internal static class SystemRefreshService
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        string? lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult
    );

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <summary>
    /// Broadcasts a WM_SETTINGCHANGE to all top-level windows, telling them
    /// to re-read their registry settings. Suitable for most system settings.
    /// </summary>
    public static void NotifySettingChange()
    {
        SendMessageTimeout(
            HWND_BROADCAST,
            WM_SETTINGCHANGE,
            IntPtr.Zero,
            null,
            SMTO_ABORTIFHUNG,
            100,
            out _
        );
    }

    /// <summary>
    /// Notifies shell/explorer that a setting affecting file view or shell behavior has changed.
    /// Equivalent to refreshing File Explorer without restarting it.
    /// </summary>
    public static void RefreshShell()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// As a last resort, if targeted refreshes don't work, we can restart Explorer
    /// </summary>
    public static void RestartExplorer()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c taskkill /f /im explorer.exe && start explorer.exe",
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    /// <summary>
    /// As a last resort, if targeted refreshes don't work, we can restart Explorer
    /// </summary>
    public static Task RestartExplorerAsync()
    {
        return Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c taskkill /f /im explorer.exe && start explorer.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        });
    }
}
