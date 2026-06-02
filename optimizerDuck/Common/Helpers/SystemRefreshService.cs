using System.Runtime.InteropServices;

namespace optimizerDuck.Common.Helpers;

internal static class SystemRefreshService
{
    #region P/Invoke declarations

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        [MarshalAs(UnmanagedType.LPWStr)] string? lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr hWndParent,
        IntPtr hWndChildAfter,
        string? lpszClass,
        string? lpszWindow
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(
        uint wEventId,
        uint uFlags,
        IntPtr dwItem1,
        IntPtr dwItem2
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        IntPtr pvParam,
        uint fWinIni
    );

    #endregion

    #region Constants

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_THEMECHANGED = 0x031A;
    private const uint WM_COMMAND = 0x0111;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;
    private const uint SHCNF_FLUSH = 0x1000;

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    private static readonly IntPtr TOGGLE_DESKTOP_ICONS = 0x7402;

    private const uint LVM_REFRESH = 0x1033;
    private const uint LVM_UPDATE = 0x102D;

    private const string PROGMAN_CLASS = "Progman";
    private const string SHELLDLL_DEFVIEW = "SHELLDLL_DefView";
    private const string SYSLISTVIEW32 = "SysListView32";
    private const string WORKERW_CLASS = "WorkerW";

    #endregion

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

    public static void NotifyTaskbarSettingChange()
    {
        SendMessageTimeout(
            HWND_BROADCAST,
            WM_SETTINGCHANGE,
            IntPtr.Zero,
            "TraySettings",
            SMTO_ABORTIFHUNG,
            100,
            out _
        );
    }

    public static void RefreshShell()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
    }

    public static void RefreshDesktop()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);

        foreach (var listView in EnumerateDesktopListViews())
        {
            SendMessage(listView, LVM_REFRESH, IntPtr.Zero, IntPtr.Zero);
            SendMessage(listView, LVM_UPDATE, IntPtr.Zero, IntPtr.Zero);

            InvalidateRect(listView, IntPtr.Zero, true);
            UpdateWindow(listView);
        }
    }

    public static void SetDesktopIconsVisible(bool showIcons)
    {
        var defView = FindDesktopDefView();
        if (defView == IntPtr.Zero)
            return;

        var listView = FindWindowEx(defView, IntPtr.Zero, SYSLISTVIEW32, null);
        var currentlyVisible = listView != IntPtr.Zero && IsWindowVisible(listView);

        if (currentlyVisible != showIcons)
            SendMessage(defView, WM_COMMAND, TOGGLE_DESKTOP_ICONS, IntPtr.Zero);

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static void RefreshDesktopIconVisibilityFromRegistry()
    {
        var showIcons = true;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                writable: false
            );
            var raw = key?.GetValue("HideIcons");
            var value = raw switch
            {
                int i => i,
                long l => (int)l,
                _ => 0,
            };
            showIcons = value == 0;
        }
        catch (System.Security.SecurityException)
        {
        }
        catch (System.IO.IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        SetDesktopIconsVisible(showIcons);
    }

    public static void UpdatePerUserSystemParameters()
    {
        SystemParametersInfo(
            SPI_SETDESKWALLPAPER,
            0,
            IntPtr.Zero,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
        );
    }

    public static void NotifyThemeChanged()
    {
        SendMessageTimeout(
            HWND_BROADCAST,
            WM_THEMECHANGED,
            IntPtr.Zero,
            null,
            SMTO_ABORTIFHUNG,
            100,
            out _
        );
    }

    private static IntPtr FindDesktopDefView()
    {
        var progman = FindWindow(PROGMAN_CLASS, null);
        if (progman != IntPtr.Zero)
        {
            var defView = FindWindowEx(progman, IntPtr.Zero, SHELLDLL_DEFVIEW, null);
            if (defView != IntPtr.Zero)
                return defView;
        }

        var worker = FindWindowEx(IntPtr.Zero, IntPtr.Zero, WORKERW_CLASS, null);
        while (worker != IntPtr.Zero)
        {
            var defView = FindWindowEx(worker, IntPtr.Zero, SHELLDLL_DEFVIEW, null);
            if (defView != IntPtr.Zero)
                return defView;

            worker = FindWindowEx(IntPtr.Zero, worker, WORKERW_CLASS, null);
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<IntPtr> EnumerateDesktopListViews()
    {
        var seen = new HashSet<IntPtr>();

        var progman = FindWindow(PROGMAN_CLASS, null);
        if (progman != IntPtr.Zero)
        {
            foreach (var lv in EnumerateListViewsUnder(progman))
            {
                if (seen.Add(lv))
                    yield return lv;
            }
        }

        var worker = FindWindowEx(IntPtr.Zero, IntPtr.Zero, WORKERW_CLASS, null);
        while (worker != IntPtr.Zero)
        {
            foreach (var lv in EnumerateListViewsUnder(worker))
            {
                if (seen.Add(lv))
                    yield return lv;
            }

            worker = FindWindowEx(IntPtr.Zero, worker, WORKERW_CLASS, null);
        }
    }

    private static IEnumerable<IntPtr> EnumerateListViewsUnder(IntPtr host)
    {
        var defView = FindWindowEx(host, IntPtr.Zero, SHELLDLL_DEFVIEW, null);
        if (defView == IntPtr.Zero)
            yield break;

        var listView = FindWindowEx(defView, IntPtr.Zero, SYSLISTVIEW32, null);
        while (listView != IntPtr.Zero)
        {
            yield return listView;
            listView = FindWindowEx(defView, listView, SYSLISTVIEW32, null);
        }
    }
}
