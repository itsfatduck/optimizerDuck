using System.Reflection;
using optimizerDuck.Domain.Customize.Categories;
using optimizerDuck.Domain.Optimizations.Categories;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Test.Domain.Optimizations;

public class CrossVersionRegistryTargetsTests
{
    private static RegistryItem[] GetDeclaredRegistryItems(DeclarativeOptimization optimization)
    {
        var property = typeof(DeclarativeOptimization).GetProperty(
            "RegistryItems",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        return (RegistryItem[])property.GetValue(optimization)!;
    }

    [Fact]
    public void DisableAutoLogger_KeepsLwtNetLogAndExcludesWdiDiagnosticLoggers()
    {
        var items = GetDeclaredRegistryItems(new SecurityAndPrivacy.DisableAutoLogger());

        Assert.Contains(
            items,
            item => item.Path.EndsWith(@"\Autologger\LwtNetLog", StringComparison.OrdinalIgnoreCase)
        );
        Assert.DoesNotContain(
            items,
            item => item.Path.EndsWith(@"\Autologger\DiagLog", StringComparison.OrdinalIgnoreCase)
        );
        Assert.DoesNotContain(
            items,
            item =>
                item.Path.EndsWith(@"\Autologger\WdiContextLog", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void DisableDiagnosticAutoLoggers_TargetsDiagLogAndWdiContextLogAndRequiresReboot()
    {
        var optimization = new SecurityAndPrivacy.DisableDiagnosticAutoLoggers();
        var items = GetDeclaredRegistryItems(optimization);

        Assert.Equal(2, items.Length);
        Assert.Contains(
            items,
            item => item.Path.EndsWith(@"\Autologger\DiagLog", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            items,
            item =>
                item.Path.EndsWith(@"\Autologger\WdiContextLog", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal(RestartRequirement.Reboot, optimization.Metadata.RequiresRestart);
    }

    [Fact]
    public void DisableBackgroundApps_Windows10_IncludesLegacySearchToggleAndPolicyKeys()
    {
        var items = Performance.DisableBackgroundApps.BuildRegistryItems(isWindows11OrGreater: false);

        Assert.Equal(3, items.Length);
        Assert.Contains(items, item => item.Name == "GlobalUserDisabled");
        Assert.Contains(items, item => item.Name == "LetAppsRunInBackground");
        Assert.Contains(items, item => item.Name == "BackgroundAppGlobalToggle");
    }

    [Fact]
    public void DisableBackgroundApps_Windows11_OmitsLegacySearchToggle()
    {
        var items = Performance.DisableBackgroundApps.BuildRegistryItems(isWindows11OrGreater: true);

        Assert.Equal(2, items.Length);
        Assert.Contains(items, item => item.Name == "GlobalUserDisabled");
        Assert.Contains(items, item => item.Name == "LetAppsRunInBackground");
        Assert.DoesNotContain(items, item => item.Name == "BackgroundAppGlobalToggle");
    }

    [Fact]
    public void DisableStartMenuWebSearch_Windows10_IncludesBingSearchEnabledAndPolicies()
    {
        var items = UserExperience.DisableStartMenuWebSearch.BuildRegistryItems(
            isWindows11OrGreater: false
        );

        Assert.Equal(4, items.Length);
        Assert.Contains(items, item => item.Name == "BingSearchEnabled");
        Assert.Contains(
            items,
            item =>
                item.Path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase)
                && item.Name == "DisableSearchBoxSuggestions"
        );
        Assert.Contains(
            items,
            item =>
                item.Path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
                && item.Name == "DisableSearchBoxSuggestions"
        );
        Assert.Contains(items, item => item.Name == "DisableWebSearch");
    }

    [Fact]
    public void DisableStartMenuWebSearch_Windows11_OmitsBingSearchEnabled()
    {
        var items = UserExperience.DisableStartMenuWebSearch.BuildRegistryItems(
            isWindows11OrGreater: true
        );

        Assert.Equal(3, items.Length);
        Assert.DoesNotContain(items, item => item.Name == "BingSearchEnabled");
        Assert.Contains(
            items,
            item =>
                item.Path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase)
                && item.Name == "DisableSearchBoxSuggestions"
        );
        Assert.Contains(
            items,
            item =>
                item.Path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
                && item.Name == "DisableSearchBoxSuggestions"
        );
        Assert.Contains(items, item => item.Name == "DisableWebSearch");
    }

    [Fact]
    public void BingSearch_Windows10Pre2004_RequiresLegacyToggleAndMakesPolicyOptional()
    {
        var toggles = Preferences.BingSearch.BuildRegistryToggles(
            isWindows11OrGreater: false,
            isWindows10_2004OrGreater: false
        );

        Assert.Equal(2, toggles.Count);
        var legacy = Assert.Single(toggles, t => t.Name == "BingSearchEnabled");
        var policy = Assert.Single(toggles, t => t.Name == "DisableSearchBoxSuggestions");
        Assert.False(legacy.IsOptional);
        Assert.True(policy.IsOptional);
    }

    [Fact]
    public void BingSearch_Windows102004AndLater_RequiresPolicyToggleAndMakesLegacyOptional()
    {
        var toggles = Preferences.BingSearch.BuildRegistryToggles(
            isWindows11OrGreater: false,
            isWindows10_2004OrGreater: true
        );

        Assert.Equal(2, toggles.Count);
        var policy = Assert.Single(toggles, t => t.Name == "DisableSearchBoxSuggestions");
        var legacy = Assert.Single(toggles, t => t.Name == "BingSearchEnabled");
        Assert.False(policy.IsOptional);
        Assert.True(legacy.IsOptional);
    }

    [Fact]
    public void BingSearch_Windows11_UsesPolicyToggleOnly()
    {
        var toggles = Preferences.BingSearch.BuildRegistryToggles(
            isWindows11OrGreater: true,
            isWindows10_2004OrGreater: true
        );

        var policy = Assert.Single(toggles);
        Assert.Equal("DisableSearchBoxSuggestions", policy.Name);
        Assert.False(policy.IsOptional);
    }
}
