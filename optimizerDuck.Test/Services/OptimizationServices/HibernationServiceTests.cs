using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services.OptimizationServices;

/// <summary>
///     Live powrprof tests. The read path is checked against the hibernation file itself; the
///     commit/removal path is only exercised when a call is refused, because actually changing the
///     hibernation file on the test host is a system change (task 5.3 verifies it by hand).
/// </summary>
public class HibernationServiceTests
{
    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator
        );
    }

    [Fact]
    public void IsHibernationFilePresent_ReportsStateWithoutThrowing()
    {
        var reported = HibernationService.IsHibernationFilePresent();

        Assert.NotNull(reported);

        // One-directional cross-check: when Windows reports no hibernation file, hiberfil.sys must
        // not be there. The reverse cannot be asserted safely - File.Exists swallows access-denied
        // on that protected system file.
        if (reported is false)
            Assert.False(File.Exists(@"C:\hiberfil.sys"));
    }

    [Fact]
    public void IsHibernationFilePresent_IsStableAcrossCalls()
    {
        Assert.Equal(
            HibernationService.IsHibernationFilePresent(),
            HibernationService.IsHibernationFilePresent()
        );
    }

    [Fact]
    public void SetHibernationFile_WithoutPrivileges_IsRefusedWithStatus()
    {
        if (IsElevated())
        {
            Assert.Skip(
                "Elevated: this would really commit or remove the hibernation file, which is a system change (task 5.3 covers it by hand)."
            );
        }

        var result = HibernationService.SetHibernationFile(present: false);

        Assert.False(result.Succeeded);
        Assert.Null(result.ExceptionText);
        // Observed on an unelevated host: 0xC0000061 (STATUS_PRIVILEGE_NOT_HELD). The documented
        // failure table lists STATUS_ACCESS_DENIED instead, so this asserts "not success" rather
        // than one specific code.
        Assert.NotEqual(0u, result.NativeStatus);
    }
}
