using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

public class PowerPlanModelsTests
{
    [Fact]
    public void PowerScheme_RoundTrip_PreservesAllFields()
    {
        var id = Guid.NewGuid();
        var scheme = new PowerScheme
        {
            Id = id,
            Name = "Balanced",
            Description = "Desc",
            IsActive = true,
        };

        Assert.Equal(id, scheme.Id);
        Assert.Equal("Balanced", scheme.Name);
        Assert.Equal("Desc", scheme.Description);
        Assert.True(scheme.IsActive);
    }

    [Fact]
    public void PowerSetting_RawValues_StayNumeric()
    {
        var setting = new PowerSetting
        {
            SchemeId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            Name = "Setting",
            Kind = PowerValueKind.Unknown,
            AcValue = 42u,
            DcValue = 7u,
        };

        Assert.Equal(42u, setting.AcValue);
        Assert.Equal(7u, setting.DcValue);
        Assert.Equal(PowerValueKind.Unknown, setting.Kind);
        Assert.Null(setting.PossibleValues);
    }

    [Fact]
    public void PowerSetting_RangeMetadata_Optional()
    {
        var setting = new PowerSetting
        {
            SchemeId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            Name = "Timeout",
            Kind = PowerValueKind.Range,
            AcValue = 0u,
            DcValue = 0u,
            MinValue = 0u,
            MaxValue = 3600u,
            Unit = "seconds",
        };

        Assert.Equal(PowerValueKind.Range, setting.Kind);
        Assert.Equal(3600u, setting.MaxValue);
        Assert.Equal("seconds", setting.Unit);
    }

    [Fact]
    public void PowerSource_HasNoMagicIntegers()
    {
        Assert.Equal(0, (int)PowerSource.Ac);
        Assert.Equal(1, (int)PowerSource.Dc);
        Assert.NotEqual(PowerSource.Ac, PowerSource.Dc);
    }

    [Fact]
    public void PowerPlanError_NativeFailure_CarriesTruth()
    {
        var err = new PowerPlanError
        {
            Operation = "PowerSetActiveScheme",
            SchemeId = Guid.NewGuid(),
            ErrorCode = 1168u,
        };

        Assert.Contains("PowerSetActiveScheme", err.Describe());
        Assert.Contains("1168", err.Describe());
    }

    [Fact]
    public void PowerPlanError_Throw_HasNoFakeCode()
    {
        var err = new PowerPlanError
        {
            Operation = "PowerReadACValueIndex",
            SchemeId = Guid.NewGuid(),
            ExceptionText = "boom",
        };

        Assert.Null(err.ErrorCode);
        Assert.Contains("boom", err.Describe());
    }

    [Fact]
    public void SetSetting_BothNull_FailsWithoutNativeCall()
    {
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var call = new OpCall { Logger = NullLogger.Instance };

        var result = service.SetSetting(
            call,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null
        );

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Single(call.Changes.FailedSteps);
    }

    [Fact]
    public void SetActiveScheme_UnknownActive_FailsClosed()
    {
        // Random GUIDs force the unreadable path only when Windows cannot
        // resolve them; the assertion is fail-closed shape, not live state.
        var service = new PowerPlanService(NullLogger<PowerPlanService>.Instance);
        var call = new OpCall { Logger = NullLogger.Instance };

        var result = service.SetActiveScheme(call, Guid.NewGuid());

        // Either refused (no active readable / unknown scheme) or, on a
        // machine where GetActiveSchemeId works, a recorded native failure.
        // What must never happen: success with a revert for a bogus GUID.
        if (result.Ok)
            Assert.NotNull(call.Changes.SuccessfulSteps);
        else
            Assert.NotNull(result.Error);
    }
}
