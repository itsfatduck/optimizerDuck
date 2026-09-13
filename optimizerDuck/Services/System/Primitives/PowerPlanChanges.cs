using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Power;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
/// Optimization-edge recorder for lean <see cref="PowerPlanService"/> results:
/// the single place turning a finished native call into a <see cref="Change"/>
/// with revert step and retry. Categories call here, never hand-roll steps.
/// Tools call the service directly and skip this entirely.
/// </summary>
public static class PowerPlanChanges
{
    public static OpResult Activate(
        OpCall call,
        PowerPlanService plans,
        Guid schemeId,
        Guid? installedSchemeId = null
    )
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(plans);

        var name = plans.GetSchemeName(schemeId) ?? schemeId.ToString();
        var activation = plans.SetActiveScheme(schemeId, call.Logger);
        var result = activation.Result;
        var previousId = activation.PreviousSchemeId;
        if (!result.Ok)
        {
            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                ServiceStrings.Format("Activate power plan {0}", name),
                false,
                null,
                result.Error,
                result.ErrorDetail,
                retryCall =>
                    Task.FromResult(Activate(retryCall, plans, schemeId, installedSchemeId))
            );
            return result;
        }

        if (previousId is null || previousId.Value == schemeId)
        {
            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                ServiceStrings.Format("Power plan {0} already active (skipped)", name),
                true
            );
            return OpResult.Success();
        }

        var step = new PowerPlanRevertStep
        {
            PreviousSchemeId = previousId.Value,
            InstalledSchemeId = installedSchemeId ?? Guid.Empty,
        };
        call.Changes.Add(
            ServiceStrings.PowerPlanName,
            ServiceStrings.Format("Activate power plan {0}", name),
            true,
            step
        );
        return OpResult.Success(step);
    }

    public static async Task<InstallResult> InstallAsync(
        OpCall call,
        PowerPlanService plans,
        string filePath,
        Guid destinationId,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(plans);

        var install = await plans
            .InstallSchemeAsync(filePath, destinationId, call.Logger, ct)
            .ConfigureAwait(false);
        if (!install.Result.Ok || install.InstalledId is null || install.PreviousId is null)
        {
            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                ServiceStrings.Format("Install power plan {0}", destinationId),
                false,
                null,
                install.Result.Error,
                install.Result.ErrorDetail,
                async retryCall =>
                    (
                        await InstallAsync(retryCall, plans, filePath, destinationId, ct)
                            .ConfigureAwait(false)
                    ).Result
            );
            return install;
        }

        var name = plans.GetSchemeName(install.InstalledId.Value) ?? install.InstalledId.ToString();
        var step = new PowerPlanRevertStep
        {
            PreviousSchemeId = install.PreviousId.Value,
            InstalledSchemeId = install.InstalledId.Value,
        };
        call.Changes.Add(
            ServiceStrings.PowerPlanName,
            ServiceStrings.Format("Install power plan {0}", name),
            true,
            step
        );
        return new InstallResult(OpResult.Success(step), install.InstalledId, install.PreviousId);
    }

    public static OpResult WriteSetting(
        OpCall call,
        PowerPlanService plans,
        Guid schemeId,
        Guid subgroupId,
        Guid settingId,
        uint? acValue,
        uint? dcValue
    )
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(plans);

        var description = ServiceStrings.Format(
            "Write power setting {0}/{1}/{2}",
            schemeId,
            subgroupId,
            settingId
        );
        var write = plans.SetSetting(
            schemeId,
            subgroupId,
            settingId,
            acValue,
            dcValue,
            call.Logger
        );
        var result = write.Result;
        var prevAc = write.PreviousAcValue;
        var prevDc = write.PreviousDcValue;
        if (!result.Ok || prevAc is null || prevDc is null)
        {
            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                description,
                false,
                null,
                result.Error,
                result.ErrorDetail,
                retryCall =>
                    Task.FromResult(
                        WriteSetting(
                            retryCall,
                            plans,
                            schemeId,
                            subgroupId,
                            settingId,
                            acValue,
                            dcValue
                        )
                    )
            );
            return result;
        }

        var step = new PowerSettingRevertStep
        {
            SchemeId = schemeId,
            SubgroupId = subgroupId,
            SettingId = settingId,
            PreviousAcValue = prevAc.Value,
            PreviousDcValue = prevDc.Value,
        };
        call.Changes.Add(ServiceStrings.PowerPlanName, description, true, step);
        return OpResult.Success(step);
    }
}
