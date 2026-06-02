namespace optimizerDuck.Domain.Customize.Models;

[Flags]
public enum CustomizeRefreshScope
{
    /// <summary>No refresh.</summary>
    None = 0,

    /// <summary>Broadcast <c>WM_SETTINGCHANGE</c> so apps re-read registry.</summary>
    Settings = 1 << 0,

    /// <summary>
    /// Notify the shell that file associations or icon cache changed
    /// (<c>SHChangeNotify(SHCNE_ASSOCCHANGED)</c>).
    /// </summary>
    Associations = 1 << 1,

    /// <summary>
    /// Force the desktop icon list (<c>SysListView32</c>) to repaint so
    /// changes to the {CLSID}-based desktop icon CLSIDs are visible immediately.
    /// </summary>
    Desktop = 1 << 2,

    /// <summary>
    /// Broadcast a taskbar-targeted <c>WM_SETTINGCHANGE</c> so the taskbar
    /// re-evaluates alignment, widgets, and other tray items.
    /// </summary>
    Taskbar = 1 << 3,

    /// <summary>
    /// Push <c>SystemParametersInfo</c> with <c>SPIF_SENDCHANGE</c> so the
    /// shell re-reads per-user system parameters (icon spacing, wallpaper,
    /// visual effects).
    /// </summary>
    PolicyUpdate = 1 << 4,

    /// <summary>Broadcast <c>WM_THEMECHANGED</c> for theme/visual tweaks.</summary>
    Theme = 1 << 5,

    /// <summary>
    /// Toggles desktop icon visibility by reading the current <c>HideIcons</c>
    /// registry value and sending <c>WM_COMMAND 0x7402</c> to the desktop's
    /// </summary>
    DesktopIconCache = 1 << 6,

    /// <summary>Standard explorer-level settings (Settings + Associations).</summary>
    Default = Settings | Associations,

    /// <summary>Toggles affecting the desktop icon list.</summary>
    DesktopIcons = Settings | Desktop,

    /// <summary>Toggling the global "hide all desktop icons" flag (HideIcons).</summary>
    HideDesktopIcons = Settings | DesktopIconCache,

    /// <summary>Toggles affecting the taskbar.</summary>
    TaskbarSettings = Settings | Taskbar,

    /// <summary>File-explorer view options (Extensions/Hidden/etc.).</summary>
    ExplorerView = Settings | Associations | PolicyUpdate,
}
