using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Power;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
/// Records the optimization edge for lean <see cref="PowerPlanService"/> results: turns a
/// finished native call into a <see cref="Change"/> with revert step and retry.
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
            call.Changes.AddSkip(
                ServiceStrings.PowerPlanName,
                ServiceStrings.Format("Power plan {0} already active (skipped)", name),
                new PowerPlanActivateDetail { PlanName = name, PreviousPlanName = name }
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
            step,
            detail: new PowerPlanActivateDetail
            {
                PlanName = name,
                PreviousPlanName =
                    plans.GetSchemeName(previousId.Value) ?? previousId.Value.ToString(),
                NewPlanName = name,
            }
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
            // The import can land even when activation fails, so the installed scheme is recorded
            // whenever Windows reports it; a revert then removes the plan this run added.
            PowerPlanRevertStep? orphan = null;
            string? orphanName = null;
            if (install.InstalledId is not null)
            {
                orphan = new PowerPlanRevertStep
                {
                    PreviousSchemeId = install.PreviousId ?? Guid.Empty,
                    InstalledSchemeId = install.InstalledId.Value,
                };
                orphanName = plans.GetSchemeName(install.InstalledId.Value);
            }

            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                ServiceStrings.Format("Install power plan {0}", destinationId),
                false,
                orphan,
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

        var name =
            plans.GetSchemeName(install.InstalledId.Value) ?? install.InstalledId.Value.ToString();
        var step = new PowerPlanRevertStep
        {
            PreviousSchemeId = install.PreviousId.Value,
            InstalledSchemeId = install.InstalledId.Value,
        };
        call.Changes.Add(
            ServiceStrings.PowerPlanName,
            ServiceStrings.Format("Install power plan {0}", name),
            true,
            step,
            detail: new PowerPlanInstallDetail { PlanName = name }
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

        // Both values are read before any write; the mains value may already be written when the
        // battery write fails, so the compensation is recorded even on failure. Restoring a value
        // that was never written is a no-op, and the entry lets a retry end at the original.
        PowerSettingRevertStep? step = null;
        string? previousPair = null;
        if (prevAc is not null && prevDc is not null)
        {
            previousPair = $"AC {prevAc.Value} / DC {prevDc.Value}";
            step = new PowerSettingRevertStep
            {
                SchemeId = schemeId,
                SubgroupId = subgroupId,
                SettingId = settingId,
                PreviousAcValue = prevAc.Value,
                PreviousDcValue = prevDc.Value,
            };
        }

        var facts = new PowerSettingDetail
        {
            SettingId = settingId.ToString(),
            SettingName = plans.GetSetting(schemeId, subgroupId, settingId)?.Name,
            PreviousValue = previousPair,
        };

        if (!result.Ok || step is null)
        {
            call.Changes.Add(
                ServiceStrings.PowerPlanName,
                description,
                false,
                step,
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
                    ),
                detail: previousPair is null ? null : facts
            );
            return result;
        }

        call.Changes.Add(
            ServiceStrings.PowerPlanName,
            description,
            true,
            step,
            detail: facts with
            {
                NewValue = $"AC {acValue} / DC {dcValue}",
            }
        );
        return OpResult.Success(step);
    }
}
