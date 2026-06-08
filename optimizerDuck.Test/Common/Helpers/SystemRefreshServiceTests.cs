using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Test.Common.Helpers;

public class SystemRefreshServiceTests
{
    [Fact]
    public void NotifySettingChange_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.NotifySettingChange());
        Assert.Null(exception);
    }

    [Fact]
    public void RefreshShell_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.RefreshShell());
        Assert.Null(exception);
    }

    [Fact]
    public void NotifySettingChange_ThenRefreshShell_DoesNotThrow()
    {
        var exception1 = Record.Exception(() => SystemRefreshService.NotifySettingChange());
        Assert.Null(exception1);

        var exception2 = Record.Exception(() => SystemRefreshService.RefreshShell());
        Assert.Null(exception2);
    }

    [Fact]
    public void NotifySettingChange_CanBeCalledMultipleTimes()
    {
        for (var i = 0; i < 5; i++)
        {
            var exception = Record.Exception(() => SystemRefreshService.NotifySettingChange());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void NotifyTaskbarSettingChange_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.NotifyTaskbarSettingChange());
        Assert.Null(exception);
    }

    [Fact]
    public void RefreshDesktop_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.RefreshDesktop());
        Assert.Null(exception);
    }

    [Fact]
    public void UpdatePerUserSystemParameters_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            SystemRefreshService.UpdatePerUserSystemParameters()
        );
        Assert.Null(exception);
    }

    [Fact]
    public void NotifyThemeChanged_DoesNotThrow()
    {
        var exception = Record.Exception(() => SystemRefreshService.NotifyThemeChanged());
        Assert.Null(exception);
    }

    [Fact]
    public void SetDesktopIconsVisible_True_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            SystemRefreshService.SetDesktopIconsVisible(showIcons: true)
        );
        Assert.Null(exception);
    }

    [Fact]
    public void SetDesktopIconsVisible_False_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            SystemRefreshService.SetDesktopIconsVisible(showIcons: false)
        );
        Assert.Null(exception);
    }

    [Fact]
    public void RefreshDesktopIconVisibilityFromRegistry_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            SystemRefreshService.RefreshDesktopIconVisibilityFromRegistry()
        );
        Assert.Null(exception);
    }

    [Fact]
    public void SetDesktopIconsVisible_ToggleBackAndForth_DoesNotThrow()
    {
        var ex1 = Record.Exception(() =>
            SystemRefreshService.SetDesktopIconsVisible(showIcons: true)
        );
        Assert.Null(ex1);

        var ex2 = Record.Exception(() =>
            SystemRefreshService.SetDesktopIconsVisible(showIcons: false)
        );
        Assert.Null(ex2);

        var ex3 = Record.Exception(() =>
            SystemRefreshService.SetDesktopIconsVisible(showIcons: true)
        );
        Assert.Null(ex3);
    }
}
