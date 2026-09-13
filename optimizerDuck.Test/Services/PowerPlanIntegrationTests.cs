using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

/// <summary>
/// Windows integration tests against real powrprof.dll. No mocks:
/// every test touches the live API and restores what it changed.
/// </summary>
public class PowerPlanIntegrationTests
{
    [Fact]
    public void ListSchemes_ContainsActiveScheme()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);

        var schemes = service.ListSchemes();
        var activeId = service.GetActiveSchemeId();

        Assert.NotNull(activeId);
        Assert.NotEmpty(schemes);
        Assert.Contains(schemes, s => s.Id == activeId.Value && s.IsActive);
        Assert.All(schemes, s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
    }

    [Fact]
    public void GetScheme_ActiveScheme_ReturnsMetadata()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var activeId = service.GetActiveSchemeId();
        Assert.NotNull(activeId);

        var scheme = service.GetScheme(activeId.Value);

        Assert.NotNull(scheme);
        Assert.Equal(activeId.Value, scheme.Id);
        Assert.True(scheme.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(scheme.Name));
    }

    [Fact]
    public void GetScheme_UnknownGuid_ReturnsNull()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);

        Assert.Null(service.GetScheme(Guid.NewGuid()));
        Assert.False(service.SchemeExists(Guid.NewGuid()));
    }

    [Fact]
    public void ListGroupsAndSettings_ActiveScheme_HasEntries()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var activeId = service.GetActiveSchemeId();
        Assert.NotNull(activeId);

        var groups = service.ListGroups(activeId.Value);

        Assert.NotEmpty(groups);
        var first = groups[0];
        var settings = service.ListSettings(activeId.Value, first.Id);
        Assert.NotNull(settings);
    }

    [Fact]
    public void SettingValue_RoundTrip_RestoresExactValues()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var activeId = service.GetActiveSchemeId();
        Assert.NotNull(activeId);

        // Find the first setting with readable AC/DC values.
        PowerSetting? target = null;
        foreach (var group in service.ListGroups(activeId.Value))
        {
            foreach (var setting in service.ListSettings(activeId.Value, group.Id))
            {
                target = setting;
                break;
            }
            if (target is not null)
                break;
        }
        Assert.NotNull(target);

        var origAc = service.GetSettingValue(
            activeId.Value,
            target.GroupId,
            target.Id,
            PowerSource.Ac
        );
        var origDc = service.GetSettingValue(
            activeId.Value,
            target.GroupId,
            target.Id,
            PowerSource.Dc
        );
        Assert.NotNull(origAc);
        Assert.NotNull(origDc);

        try
        {
            var call = new optimizerDuck.Domain.Execution.OpCall { Logger = NullLogger.Instance };
            // Write back the same values: exercises the write path with no semantic change.
            var write = service.SetSetting(
                call,
                activeId.Value,
                target.GroupId,
                target.Id,
                origAc.Value,
                origDc.Value
            );
            Assert.True(write.Ok);

            var rereadAc = service.GetSettingValue(
                activeId.Value,
                target.GroupId,
                target.Id,
                PowerSource.Ac
            );
            var rereadDc = service.GetSettingValue(
                activeId.Value,
                target.GroupId,
                target.Id,
                PowerSource.Dc
            );
            Assert.Equal(origAc.Value, rereadAc);
            Assert.Equal(origDc.Value, rereadDc);
        }
        finally
        {
            var call = new optimizerDuck.Domain.Execution.OpCall { Logger = NullLogger.Instance };
            service.SetSetting(
                call,
                activeId.Value,
                target.GroupId,
                target.Id,
                origAc.Value,
                origDc.Value
            );
        }
    }

    [Fact]
    public void DeleteScheme_ActiveScheme_Refused()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var activeId = service.GetActiveSchemeId();
        Assert.NotNull(activeId);
        var call = new optimizerDuck.Domain.Execution.OpCall { Logger = NullLogger.Instance };

        var result = service.DeleteScheme(call, activeId.Value);

        Assert.False(result.Ok);
    }

    [Fact]
    public void DuplicateAndDelete_Lifecycle_RoundTrips()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var activeId = service.GetActiveSchemeId();
        Assert.NotNull(activeId);
        var call = new optimizerDuck.Domain.Execution.OpCall { Logger = NullLogger.Instance };

        var (dupResult, dupId) = service.DuplicateScheme(call, activeId.Value);
        Assert.True(dupResult.Ok);
        Assert.NotNull(dupId);
        Assert.True(service.SchemeExists(dupId.Value));

        try
        {
            var rename = service.SetSchemeName(call, dupId.Value, "optimizerDuck test plan");
            Assert.True(rename.Ok);
            var renamed = service.GetScheme(dupId.Value);
            Assert.Equal("optimizerDuck test plan", renamed?.Name);

            var describe = service.SetSchemeDescription(call, dupId.Value, "test description");
            Assert.True(describe.Ok);
        }
        finally
        {
            var del = service.DeleteScheme(call, dupId.Value);
            Assert.True(del.Ok);
            Assert.False(service.SchemeExists(dupId.Value));
        }
    }
}
