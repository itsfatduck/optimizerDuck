using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
///     Hibernation file state and commit/removal over the documented power information callback
///     (<c>CallNtPowerInformation</c>), so the optimization no longer shells out to
///     <c>powercfg /h</c>. Level 4 returns the system capabilities that carry the previous state,
///     level 10 commits or removes the hibernation file.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HibernationService
{
    private const int SystemPowerCapabilities = 4;
    private const int SystemReserveHiberFile = 10;

    /// <summary>
    ///     Comfortably larger than the native <c>SYSTEM_POWER_CAPABILITIES</c> structure (~88
    ///     bytes); the callback writes only its own fields, so an oversized buffer is safe and
    ///     avoids modelling the whole structure.
    /// </summary>
    private const int CapabilitiesBufferSize = 160;

    /// <summary>
    ///     Byte offset of <c>HiberFilePresent</c>: the documented structure opens with nine
    ///     one-byte <c>BOOLEAN</c> members (PowerButtonPresent, SleepButtonPresent, LidPresent,
    ///     SystemS1-S5, HiberFilePresent), so there is no padding before it.
    /// </summary>
    private const int HiberFilePresentOffset = 8;

    private const uint StatusSuccess = 0;

    /// <summary>Result of a hibernation file commit/removal request, with the raw NTSTATUS.</summary>
    /// <param name="Succeeded">Whether the callback reported STATUS_SUCCESS.</param>
    /// <param name="NativeStatus">The NTSTATUS value, so callers can log the real code.</param>
    /// <param name="ExceptionText">Throw text when the interop itself failed, else null.</param>
    public sealed record HibernationChangeResult(
        bool Succeeded,
        uint NativeStatus,
        string? ExceptionText = null
    );

    /// <summary>
    ///     Whether the system reports a hibernation file right now, which is what hibernation and
    ///     Fast Startup depend on. Null when the capabilities could not be read, so a caller can
    ///     keep its own fail-safe default instead of treating "unknown" as "absent".
    /// </summary>
    public static bool? IsHibernationFilePresent()
    {
        var buffer = Marshal.AllocHGlobal(CapabilitiesBufferSize);
        try
        {
            var status = CallNtPowerInformation(
                SystemPowerCapabilities,
                IntPtr.Zero,
                0,
                buffer,
                CapabilitiesBufferSize
            );

            if (status != StatusSuccess)
                return null;

            return Marshal.ReadByte(buffer, HiberFilePresentOffset) != 0;
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    ///     Commits (<paramref name="present" /> true) or removes (false) the hibernation file, the
    ///     documented equivalent of <c>powercfg /h on|off</c>. Needs sufficient privileges; a
    ///     refusal comes back as a failed result carrying the NTSTATUS instead of throwing.
    /// </summary>
    public static HibernationChangeResult SetHibernationFile(bool present)
    {
        try
        {
            var request = (byte)(present ? 1 : 0);
            var status = CallNtPowerInformation(
                SystemReserveHiberFile,
                ref request,
                1,
                IntPtr.Zero,
                0
            );

            return new HibernationChangeResult(status == StatusSuccess, unchecked((uint)status));
        }
        catch (Exception ex)
        {
            return new HibernationChangeResult(false, 0, ex.Message);
        }
    }

    [DllImport("powrprof.dll")]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        IntPtr outputBuffer,
        uint outputBufferLength
    );

    [DllImport("powrprof.dll")]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        ref byte inputBuffer,
        uint inputBufferLength,
        IntPtr outputBuffer,
        uint outputBufferLength
    );
}
