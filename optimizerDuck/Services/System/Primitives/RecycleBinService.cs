using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Recycle Bin size/count and emptying over shell32, without launching PowerShell.
///     Layout note: <c>SHQUERYRBINFO</c> is a default-aligned 24-byte structure. Verified
///     against live shell32: <c>cbSize</c> 20 is refused with E_INVALIDARG while 24 is accepted,
///     so this must NOT be declared with <c>Pack = 1</c> the way older samples do.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RecycleBinService
{
    private const int S_OK = 0;

    // Documented prompt suppression for SHEmptyRecycleBin.
    internal const uint ShrbNoConfirmation = 0x00000001;
    internal const uint ShrbNoProgressUi = 0x00000002;
    internal const uint ShrbNoSound = 0x00000004;
    internal const uint SuppressionFlags = ShrbNoConfirmation | ShrbNoProgressUi | ShrbNoSound;

    /// <summary>Totals for one Recycle Bin scope: bytes and item count as the shell reports them.</summary>
    public sealed record RecycleBinTotals(long SizeBytes, long ItemCount);

    /// <summary>
    ///     Reads the caller's Recycle Bin totals. <paramref name="rootPath" /> empty means every
    ///     drive (the documented <c>L""</c> form); a drive root such as <c>C:\</c> narrows it.
    ///     Returns zeros when the shell refuses the path, so callers can render without a failure
    ///     branch.
    /// </summary>
    public static RecycleBinTotals Query(string rootPath = "")
    {
        var info = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
        if (SHQueryRecycleBin(rootPath, ref info) != S_OK)
            return new RecycleBinTotals(0, 0);

        return new RecycleBinTotals(info.i64Size, info.i64NumItems);
    }

    /// <summary>
    ///     Empties the Recycle Bin for <paramref name="rootPath" /> (empty = every drive) with
    ///     confirmation, progress UI, and sound suppressed. Irreversible: callers must not record
    ///     a revert step for it.
    /// </summary>
    /// <returns>Whether the shell reported success, plus the HRESULT when it did not.</returns>
    public static (bool Succeeded, int ErrorCode) Empty(string rootPath = "")
    {
        var hr = SHEmptyRecycleBin(IntPtr.Zero, rootPath, SuppressionFlags);
        return (hr == S_OK, hr);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport(
        "shell32.dll",
        EntryPoint = "SHQueryRecycleBinW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode
    )]
    private static extern int SHQueryRecycleBin(
        string pszRootPath,
        ref SHQUERYRBINFO pSHQueryRBInfo
    );

    [DllImport(
        "shell32.dll",
        EntryPoint = "SHEmptyRecycleBinW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode
    )]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
}
