using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Power;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Services.System;

/// Power scheme operations over powrprof (no child processes).
/// Single owner of every power P/Invoke in the app.
/// Fast Win32 calls run synchronously on the caller thread; only the
/// file-I/O-bound import is async. Reads take ids only; mutating paths take
/// an optional <see cref="ILogger"/> (default null), return <see cref="OpResult"/>
/// with native diagnostics, and record nothing: revert recording lives at the
/// optimization edge (see PowerPlanChanges), so tools reuse the same core.
/// Import semantics: a non-empty <c>destinationId</c> pre-seeds the GUID**
/// slot so Windows imports under it (replacing any scheme with the same
/// GUID, same as <c>powercfg /import file guid</c>); an empty GUID passes
/// NULL and Windows allocates one, returned to the caller.
/// Hibernate (<c>powercfg /h</c>) is intentionally NOT here: it disables the
/// hibernation feature and deletes hiberfil.sys, and no supported native
/// power-scheme API covers that. It stays a documented shell exception in
/// <c>DisableHibernateAndFastStartup</c>.
/// </summary>
[SupportedOSPlatform("windows")]
// Virtual members are a hand-double seam for tests (no mocking libraries).
public class PowerPlanService
{
    private const uint ERROR_SUCCESS = 0;
    private const uint ERROR_MORE_DATA = 234;
    private const uint ACCESS_SCHEME = 16;
    private const uint ACCESS_SUBGROUP = 17;
    private const uint ACCESS_INDIVIDUAL_SETTING = 18;

    private readonly ILogger<PowerPlanService> _logger;

    public PowerPlanService(ILogger<PowerPlanService> logger)
    {
        _logger = logger;
    }

    public virtual Guid? GetActiveSchemeId()
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out ptr) != ERROR_SUCCESS || ptr == IntPtr.Zero)
                return null;
            return Marshal.PtrToStructure<Guid>(ptr);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read active power scheme");
            return null;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                LocalFree(ptr);
        }
    }

    public IReadOnlyList<PowerScheme> ListSchemes()
    {
        var schemes = new List<PowerScheme>();
        try
        {
            var active = GetActiveSchemeId();
            foreach (var id in EnumerateGuids(ACCESS_SCHEME, null, null))
                schemes.Add(ReadScheme(id, active == id));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate power schemes");
        }
        return schemes;
    }

    public PowerScheme? GetScheme(Guid schemeId)
    {
        var id = schemeId;
        var active = GetActiveSchemeId();
        var name = GetSchemeName(id);
        if (name is null && !SchemeExists(id))
            return null;
        return new PowerScheme
        {
            Id = id,
            Name = name ?? id.ToString(),
            Description = GetSchemeDescription(id),
            IsActive = active == id,
        };
    }

    public bool SchemeExists(Guid schemeId)
    {
        // No direct 'exists' API: a friendly-name read succeeds only for
        // real schemes, so reuse it instead of guessing from a list.
        try
        {
            return ReadSchemeNameRaw(schemeId) is not null;
        }
        catch
        {
            return false;
        }
    }

    public string? GetSchemeName(Guid schemeId)
    {
        try
        {
            return ReadSchemeNameRaw(schemeId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read power scheme name {SchemeId}", schemeId);
            return null;
        }
    }

    public string? GetSchemeDescription(Guid schemeId)
    {
        try
        {
            return ReadSchemeDescriptionRaw(schemeId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read power scheme description {SchemeId}", schemeId);
            return null;
        }
    }

    public IReadOnlyList<PowerSettingGroup> ListGroups(Guid schemeId)
    {
        var groups = new List<PowerSettingGroup>();
        try
        {
            foreach (var id in EnumerateGuids(ACCESS_SUBGROUP, schemeId, null))
            {
                var gid = id;
                groups.Add(
                    new PowerSettingGroup
                    {
                        SchemeId = schemeId,
                        Id = gid,
                        Name = ReadGroupNameRaw(schemeId, gid) ?? gid.ToString(),
                        Description = ReadGroupDescriptionRaw(schemeId, gid),
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate subgroups for {SchemeId}", schemeId);
        }
        return groups;
    }

    public IReadOnlyList<PowerSetting> ListSettings(Guid schemeId, Guid subgroupId)
    {
        var settings = new List<PowerSetting>();
        try
        {
            foreach (var id in EnumerateGuids(ACCESS_INDIVIDUAL_SETTING, schemeId, subgroupId))
            {
                var setting = ReadSetting(schemeId, subgroupId, id);
                if (setting is not null)
                    settings.Add(setting);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to enumerate settings for {SchemeId}/{SubgroupId}",
                schemeId,
                subgroupId
            );
        }
        return settings;
    }

    public PowerSetting? GetSetting(Guid schemeId, Guid subgroupId, Guid settingId)
    {
        try
        {
            return ReadSetting(schemeId, subgroupId, settingId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to read setting {SchemeId}/{SubgroupId}/{SettingId}",
                schemeId,
                subgroupId,
                settingId
            );
            return null;
        }
    }

    public uint? GetSettingValue(Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source)
    {
        try
        {
            return TryReadRaw(schemeId, subgroupId, settingId, source);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to read {Source} value for {SchemeId}/{SubgroupId}/{SettingId}",
                source,
                schemeId,
                subgroupId,
                settingId
            );
            return null;
        }
    }

    public virtual SettingWriteResult SetSetting(
        Guid schemeId,
        Guid subgroupId,
        Guid settingId,
        uint? acValue,
        uint? dcValue,
        ILogger? logger = null
    )
    {
        if (acValue is null && dcValue is null)
        {
            const string error = "No value to write: both AC and DC are null.";
            return new SettingWriteResult(OpResult.Fail(error, error), null, null);
        }

        var prevAc = TryReadRaw(schemeId, subgroupId, settingId, PowerSource.Ac);
        var prevDc = TryReadRaw(schemeId, subgroupId, settingId, PowerSource.Dc);
        if (prevAc is null || prevDc is null)
        {
            // Fail closed: never assume an unreadable previous value is zero.
            var missing = prevAc is null ? PowerSource.Ac : PowerSource.Dc;
            var error = ServiceStrings.Format(
                "Could not read previous {0} value for {1}/{2}/{3}; refusing to write.",
                missing,
                schemeId,
                subgroupId,
                settingId
            );
            logger?.LogError(
                "Refusing power-setting write: previous {Source} value unreadable for {SchemeId}/{SubgroupId}/{SettingId}",
                missing,
                schemeId,
                subgroupId,
                settingId
            );
            return new SettingWriteResult(OpResult.Fail(error, error), null, null);
        }

        var failure = WriteValues(schemeId, subgroupId, settingId, acValue, dcValue);
        if (failure is not null)
            return new SettingWriteResult(
                OpResult.Fail(failure.Describe(), failure.Describe()),
                null,
                null
            );

        logger?.LogInformation(
            "Wrote power setting {SchemeId}/{SubgroupId}/{SettingId} AC={Ac} DC={Dc}",
            schemeId,
            subgroupId,
            settingId,
            acValue?.ToString() ?? "(unchanged)",
            dcValue?.ToString() ?? "(unchanged)"
        );
        return new SettingWriteResult(OpResult.Success(), prevAc, prevDc);
    }

    public virtual ActivationResult SetActiveScheme(Guid schemeId, ILogger? logger = null)
    {
        var previous = GetActiveSchemeId();
        if (previous is null)
        {
            const string error = "Could not read the active power plan; refusing to switch.";
            logger?.LogError("Refusing power-plan switch: active scheme unreadable");
            return new ActivationResult(OpResult.Fail(error, error), null);
        }

        if (previous.Value == schemeId)
        {
            var skippedName = GetSchemeName(schemeId) ?? schemeId.ToString();
            logger?.LogInformation("Power plan {Name} already active, skipping", skippedName);
            return new ActivationResult(OpResult.Success(), previous);
        }

        uint code;
        try
        {
            var id = schemeId;
            code = PowerSetActiveScheme(IntPtr.Zero, ref id);
        }
        catch (Exception ex)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerSetActiveScheme),
                SchemeId = schemeId,
                ExceptionText = ex.Message,
            };
            logger?.LogWarning(ex, "PowerSetActiveScheme threw for {SchemeId}", schemeId);
            return new ActivationResult(OpResult.Fail(err.Describe(), ex.ToString()), null);
        }

        if (code != ERROR_SUCCESS)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerSetActiveScheme),
                SchemeId = schemeId,
                ErrorCode = code,
            };
            logger?.LogWarning(
                "PowerSetActiveScheme failed for {SchemeId}: Win32 {ErrorCode}",
                schemeId,
                code
            );
            return new ActivationResult(OpResult.Fail(err.Describe(), err.Describe()), null);
        }

        return new ActivationResult(OpResult.Success(), previous);
    }

    public virtual OpResult DeleteScheme(Guid schemeId, ILogger? logger = null)
    {
        var name = GetSchemeName(schemeId) ?? schemeId.ToString();

        if (GetActiveSchemeId() == schemeId)
        {
            var error = ServiceStrings.Format(
                "Refusing to delete the active power plan {0}; activate another plan first.",
                name
            );
            logger?.LogError("Refusing to delete active power plan {SchemeId}", schemeId);
            return OpResult.Fail(error, error);
        }

        uint code;
        try
        {
            var id = schemeId;
            code = PowerDeleteScheme(IntPtr.Zero, ref id);
        }
        catch (Exception ex)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerDeleteScheme),
                SchemeId = schemeId,
                ExceptionText = ex.Message,
            };
            logger?.LogWarning(ex, "PowerDeleteScheme threw for {SchemeId}", schemeId);
            return OpResult.Fail(err.Describe(), ex.ToString());
        }

        if (code != ERROR_SUCCESS)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerDeleteScheme),
                SchemeId = schemeId,
                ErrorCode = code,
            };
            logger?.LogWarning(
                "PowerDeleteScheme failed for {SchemeId}: Win32 {ErrorCode}",
                schemeId,
                code
            );
            return OpResult.Fail(err.Describe(), err.Describe());
        }

        // Deleted schemes are gone: no revert step can restore them.
        return OpResult.Success();
    }

    public virtual async Task<SchemeRefResult> ImportSchemeAsync(
        string filePath,
        Guid destinationId,
        ILogger? logger = null,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
        {
            var missing = new PowerPlanError
            {
                Operation = nameof(PowerImportPowerScheme),
                Path = filePath,
                ExceptionText = "File not found.",
            };
            return new SchemeRefResult(OpResult.Fail(missing.Describe(), missing.Describe()), null);
        }

        // Genuine async boundary: file I/O, not a faked-async native call.
        await Task.Yield();

        IntPtr ppGuid = IntPtr.Zero;
        IntPtr slot = IntPtr.Zero;
        try
        {
            ppGuid = Marshal.AllocHGlobal(IntPtr.Size);
            if (destinationId != Guid.Empty)
            {
                slot = Marshal.AllocHGlobal(16);
                Marshal.StructureToPtr(destinationId, slot, false);
                Marshal.WriteIntPtr(ppGuid, slot);
            }
            else
            {
                Marshal.WriteIntPtr(ppGuid, IntPtr.Zero);
            }

            var code = PowerImportPowerScheme(IntPtr.Zero, filePath, ppGuid);
            var outSlot = Marshal.ReadIntPtr(ppGuid);
            Guid? imported =
                outSlot == IntPtr.Zero ? null : (Guid?)Marshal.PtrToStructure<Guid>(outSlot);
            if (outSlot != IntPtr.Zero && outSlot != slot)
                LocalFree(outSlot);

            if (code != ERROR_SUCCESS || imported is null)
            {
                var err = new PowerPlanError
                {
                    Operation = nameof(PowerImportPowerScheme),
                    Path = filePath,
                    ErrorCode = code,
                };
                logger?.LogWarning(
                    "PowerImportPowerScheme failed for {Path}: Win32 {ErrorCode}",
                    filePath,
                    code
                );
                return new SchemeRefResult(OpResult.Fail(err.Describe(), err.Describe()), null);
            }
            return new SchemeRefResult(OpResult.Success(), imported);
        }
        catch (Exception ex)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerImportPowerScheme),
                Path = filePath,
                ExceptionText = ex.Message,
            };
            logger?.LogWarning(ex, "PowerImportPowerScheme threw for {Path}", filePath);
            return new SchemeRefResult(OpResult.Fail(err.Describe(), ex.ToString()), null);
        }
        finally
        {
            if (slot != IntPtr.Zero)
                Marshal.FreeHGlobal(slot);
            if (ppGuid != IntPtr.Zero)
                Marshal.FreeHGlobal(ppGuid);
        }
    }

    /// <summary>
    /// Atomic install: capture previous, import file, activate. Returns the
    /// installed and previous ids together so the caller records revert once.
    /// Import and activation failures stay distinguishable by operation name.
    /// </summary>
    public virtual async Task<InstallResult> InstallSchemeAsync(
        string filePath,
        Guid destinationId,
        ILogger? logger = null,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();

        var previous = GetActiveSchemeId();
        if (previous is null)
        {
            const string error = "Could not read the active power plan; refusing to install.";
            logger?.LogError("Refusing power-plan install: active scheme unreadable");
            return new InstallResult(OpResult.Fail(error, error), null, null);
        }

        var import = await ImportSchemeAsync(filePath, destinationId, logger, ct)
            .ConfigureAwait(false);
        if (!import.Result.Ok || import.SchemeId is null)
            return new InstallResult(import.Result, null, previous);

        var activation = SetActiveScheme(import.SchemeId.Value, logger);
        if (!activation.Result.Ok)
            return new InstallResult(activation.Result, import.SchemeId, previous);

        logger?.LogInformation(
            "Installed power plan {InstalledId} (previous {PreviousId})",
            import.SchemeId,
            previous
        );
        return new InstallResult(OpResult.Success(), import.SchemeId, previous);
    }

    public virtual SchemeRefResult DuplicateScheme(Guid sourceId, ILogger? logger = null)
    {
        IntPtr ppGuid = IntPtr.Zero;
        try
        {
            // NULL seed: Windows allocates the new GUID; caller frees via LocalFree.
            ppGuid = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(ppGuid, IntPtr.Zero);

            var src = sourceId;
            var code = PowerDuplicateScheme(IntPtr.Zero, ref src, ppGuid);
            var outSlot = Marshal.ReadIntPtr(ppGuid);
            Guid? duplicated =
                outSlot == IntPtr.Zero ? null : (Guid?)Marshal.PtrToStructure<Guid>(outSlot);
            if (outSlot != IntPtr.Zero)
                LocalFree(outSlot);

            if (code != ERROR_SUCCESS || duplicated is null)
            {
                var err = new PowerPlanError
                {
                    Operation = nameof(PowerDuplicateScheme),
                    SchemeId = sourceId,
                    ErrorCode = code,
                };
                logger?.LogWarning(
                    "PowerDuplicateScheme failed for {SchemeId}: Win32 {ErrorCode}",
                    sourceId,
                    code
                );
                return new SchemeRefResult(OpResult.Fail(err.Describe(), err.Describe()), null);
            }

            return new SchemeRefResult(OpResult.Success(), duplicated);
        }
        catch (Exception ex)
        {
            var err = new PowerPlanError
            {
                Operation = nameof(PowerDuplicateScheme),
                SchemeId = sourceId,
                ExceptionText = ex.Message,
            };
            logger?.LogWarning(ex, "PowerDuplicateScheme threw for {SchemeId}", sourceId);
            return new SchemeRefResult(OpResult.Fail(err.Describe(), ex.ToString()), null);
        }
        finally
        {
            if (ppGuid != IntPtr.Zero)
                Marshal.FreeHGlobal(ppGuid);
        }
    }

    public OpResult SetSchemeName(Guid schemeId, string name, ILogger? logger = null)
    {
        var bytes = Encoding.Unicode.GetBytes(name + '\0');
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            var id = schemeId;
            var code = PowerWriteFriendlyName(
                IntPtr.Zero,
                ref id,
                IntPtr.Zero,
                IntPtr.Zero,
                buffer,
                (uint)bytes.Length
            );
            if (code != ERROR_SUCCESS)
                return FailTextWrite(nameof(PowerWriteFriendlyName), schemeId, code, null, logger);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            return FailTextWrite(nameof(PowerWriteFriendlyName), schemeId, null, ex, logger);
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    public OpResult SetSchemeDescription(
        Guid schemeId,
        string descriptionText,
        ILogger? logger = null
    )
    {
        var bytes = Encoding.Unicode.GetBytes(descriptionText + '\0');
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            var id = schemeId;
            var code = PowerWriteDescription(
                IntPtr.Zero,
                ref id,
                IntPtr.Zero,
                IntPtr.Zero,
                buffer,
                (uint)bytes.Length
            );
            if (code != ERROR_SUCCESS)
                return FailTextWrite(nameof(PowerWriteDescription), schemeId, code, null, logger);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            return FailTextWrite(nameof(PowerWriteDescription), schemeId, null, ex, logger);
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    private static OpResult FailTextWrite(
        string operation,
        Guid schemeId,
        uint? code,
        Exception? ex,
        ILogger? logger
    )
    {
        var err = new PowerPlanError
        {
            Operation = operation,
            SchemeId = schemeId,
            ErrorCode = code,
            ExceptionText = ex?.Message,
        };
        var detail = ex?.ToString() ?? err.Describe();
        logger?.LogWarning(
            ex,
            "{Operation} failed for {SchemeId}: {Detail}",
            operation,
            schemeId,
            err.Describe()
        );
        return OpResult.Fail(err.Describe(), detail);
    }

    private PowerScheme ReadScheme(Guid id, bool isActive)
    {
        return new PowerScheme
        {
            Id = id,
            Name = ReadSchemeNameRaw(id) ?? id.ToString(),
            Description = ReadSchemeDescriptionRaw(id),
            IsActive = isActive,
        };
    }

    private PowerSetting? ReadSetting(Guid schemeId, Guid subgroupId, Guid settingId)
    {
        var name = ReadSettingNameRaw(schemeId, subgroupId, settingId);
        if (name is null)
            return null;

        var ac = TryReadRaw(schemeId, subgroupId, settingId, PowerSource.Ac);
        var dc = TryReadRaw(schemeId, subgroupId, settingId, PowerSource.Dc);
        if (ac is null || dc is null)
            return null;

        var description = ReadSettingDescriptionRaw(schemeId, subgroupId, settingId);

        var kind = PowerValueKind.Unknown;
        uint? min = null;
        uint? max = null;
        uint? incr = null;
        string? unit = null;
        IReadOnlyList<PowerPossibleValue>? possible = null;
        try
        {
            var sg = subgroupId;
            var st = settingId;
            if (PowerIsSettingRangeDefined(ref sg, ref st))
            {
                kind = PowerValueKind.Range;
                if (PowerReadValueMin(IntPtr.Zero, ref sg, ref st, out var lo) == ERROR_SUCCESS)
                    min = lo;
                if (PowerReadValueMax(IntPtr.Zero, ref sg, ref st, out var hi) == ERROR_SUCCESS)
                    max = hi;
                if (
                    PowerReadValueIncrement(IntPtr.Zero, ref sg, ref st, out var step)
                    == ERROR_SUCCESS
                )
                    incr = step;
                unit = ReadUnitsRaw(subgroupId, settingId);
            }
            else
            {
                possible = ReadPossibleValues(subgroupId, settingId);
                if (possible is { Count: > 0 })
                    kind = PowerValueKind.Indexed;
            }
        }
        catch
        {
            // Metadata is best-effort: raw values above already succeeded.
        }

        return new PowerSetting
        {
            SchemeId = schemeId,
            GroupId = subgroupId,
            Id = settingId,
            Name = name,
            Description = description,
            Kind = kind,
            AcValue = ac.Value,
            DcValue = dc.Value,
            MinValue = min,
            MaxValue = max,
            Increment = incr,
            Unit = unit,
            PossibleValues = possible,
        };
    }

    private static IReadOnlyList<PowerPossibleValue>? ReadPossibleValues(
        Guid subgroupId,
        Guid settingId
    )
    {
        var list = new List<PowerPossibleValue>();
        try
        {
            for (uint index = 0; index < 512; index++)
            {
                var name = ReadPossibleNameRaw(subgroupId, settingId, index);
                if (name is null)
                    break;
                var value = ReadPossibleValueRaw(subgroupId, settingId, index);
                if (value is null)
                    break;
                list.Add(
                    new PowerPossibleValue
                    {
                        Index = index,
                        Value = value.Value,
                        Name = name,
                        Description = ReadPossibleDescriptionRaw(subgroupId, settingId, index),
                    }
                );
            }
        }
        catch
        {
            return list.Count > 0 ? list : null;
        }
        return list.Count > 0 ? list : null;
    }

    private uint? TryReadRaw(Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source)
    {
        try
        {
            var s = schemeId;
            var g = subgroupId;
            var st = settingId;
            uint value;
            var code =
                source == PowerSource.Ac
                    ? PowerReadACValueIndex(IntPtr.Zero, ref s, ref g, ref st, out value)
                    : PowerReadDCValueIndex(IntPtr.Zero, ref s, ref g, ref st, out value);
            return code == ERROR_SUCCESS ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static uint? ReadPossibleValueRaw(Guid subgroupId, Guid settingId, uint index)
    {
        var g = subgroupId;
        var st = settingId;
        uint size = 4;
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            var code = PowerReadPossibleValue(
                IntPtr.Zero,
                ref g,
                ref st,
                out _,
                index,
                buffer,
                ref size
            );
            if (code != ERROR_SUCCESS)
                return null;
            return (uint)Marshal.ReadInt32(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private PowerPlanError? WriteValues(
        Guid schemeId,
        Guid subgroupId,
        Guid settingId,
        uint? acValue,
        uint? dcValue
    )
    {
        try
        {
            if (acValue.HasValue)
            {
                var s = schemeId;
                var g = subgroupId;
                var st = settingId;
                var code = PowerWriteACValueIndex(IntPtr.Zero, ref s, ref g, ref st, acValue.Value);
                if (code != ERROR_SUCCESS)
                    return new PowerPlanError
                    {
                        Operation = nameof(PowerWriteACValueIndex),
                        SchemeId = schemeId,
                        SubgroupId = subgroupId,
                        SettingId = settingId,
                        Source = PowerSource.Ac,
                        ErrorCode = code,
                    };
            }
            if (dcValue.HasValue)
            {
                var s = schemeId;
                var g = subgroupId;
                var st = settingId;
                var code = PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref g, ref st, dcValue.Value);
                if (code != ERROR_SUCCESS)
                    return new PowerPlanError
                    {
                        Operation = nameof(PowerWriteDCValueIndex),
                        SchemeId = schemeId,
                        SubgroupId = subgroupId,
                        SettingId = settingId,
                        Source = PowerSource.Dc,
                        ErrorCode = code,
                    };
            }
            // Writes to the active scheme need reactivation to take effect.
            if (GetActiveSchemeId() == schemeId)
            {
                var s = schemeId;
                PowerSetActiveScheme(IntPtr.Zero, ref s);
            }
            return null;
        }
        catch (Exception ex)
        {
            return new PowerPlanError
            {
                Operation = "PowerWriteValueIndex",
                SchemeId = schemeId,
                SubgroupId = subgroupId,
                SettingId = settingId,
                ExceptionText = ex.Message,
            };
        }
    }

    private static string? ReadSchemeNameRaw(Guid schemeId)
    {
        var id = schemeId;
        uint size = 0;
        var probe = PowerReadFriendlyName(
            IntPtr.Zero,
            ref id,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => SchemeNameInto(ref id, buf, ref len)
        );
    }

    private static uint SchemeNameInto(ref Guid schemeId, IntPtr buffer, ref uint size)
    {
        return PowerReadFriendlyName(
            IntPtr.Zero,
            ref schemeId,
            IntPtr.Zero,
            IntPtr.Zero,
            buffer,
            ref size
        );
    }

    private static string? ReadSchemeDescriptionRaw(Guid schemeId)
    {
        var id = schemeId;
        uint size = 0;
        var probe = PowerReadDescription(
            IntPtr.Zero,
            ref id,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => SchemeDescriptionInto(ref id, buf, ref len)
        );
    }

    private static uint SchemeDescriptionInto(ref Guid schemeId, IntPtr buffer, ref uint size)
    {
        return PowerReadDescription(
            IntPtr.Zero,
            ref schemeId,
            IntPtr.Zero,
            IntPtr.Zero,
            buffer,
            ref size
        );
    }

    private static string? ReadGroupNameRaw(Guid schemeId, Guid subgroupId)
    {
        var s = schemeId;
        var g = subgroupId;
        uint size = 0;
        var probe = PowerReadFriendlyName(
            IntPtr.Zero,
            ref s,
            ref g,
            IntPtr.Zero,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => GroupNameInto(ref s, ref g, buf, ref len)
        );
    }

    private static uint GroupNameInto(
        ref Guid schemeId,
        ref Guid subgroupId,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadFriendlyName(
            IntPtr.Zero,
            ref schemeId,
            ref subgroupId,
            IntPtr.Zero,
            buffer,
            ref size
        );
    }

    private static string? ReadGroupDescriptionRaw(Guid schemeId, Guid subgroupId)
    {
        var s = schemeId;
        var g = subgroupId;
        uint size = 0;
        var probe = PowerReadDescription(
            IntPtr.Zero,
            ref s,
            ref g,
            IntPtr.Zero,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => GroupDescriptionInto(ref s, ref g, buf, ref len)
        );
    }

    private static uint GroupDescriptionInto(
        ref Guid schemeId,
        ref Guid subgroupId,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadDescription(
            IntPtr.Zero,
            ref schemeId,
            ref subgroupId,
            IntPtr.Zero,
            buffer,
            ref size
        );
    }

    private static string? ReadSettingNameRaw(Guid schemeId, Guid subgroupId, Guid settingId)
    {
        var s = schemeId;
        var g = subgroupId;
        var st = settingId;
        uint size = 0;
        var probe = PowerReadFriendlyName(IntPtr.Zero, ref s, ref g, ref st, IntPtr.Zero, ref size);
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => SettingNameInto(ref s, ref g, ref st, buf, ref len)
        );
    }

    private static uint SettingNameInto(
        ref Guid schemeId,
        ref Guid subgroupId,
        ref Guid settingId,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadFriendlyName(
            IntPtr.Zero,
            ref schemeId,
            ref subgroupId,
            ref settingId,
            buffer,
            ref size
        );
    }

    private static string? ReadSettingDescriptionRaw(Guid schemeId, Guid subgroupId, Guid settingId)
    {
        var s = schemeId;
        var g = subgroupId;
        var st = settingId;
        uint size = 0;
        var probe = PowerReadDescription(IntPtr.Zero, ref s, ref g, ref st, IntPtr.Zero, ref size);
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => SettingDescriptionInto(ref s, ref g, ref st, buf, ref len)
        );
    }

    private static uint SettingDescriptionInto(
        ref Guid schemeId,
        ref Guid subgroupId,
        ref Guid settingId,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadDescription(
            IntPtr.Zero,
            ref schemeId,
            ref subgroupId,
            ref settingId,
            buffer,
            ref size
        );
    }

    private static string? ReadUnitsRaw(Guid subgroupId, Guid settingId)
    {
        var g = subgroupId;
        var st = settingId;
        uint size = 0;
        var probe = PowerReadValueUnitsSpecifier(IntPtr.Zero, ref g, ref st, IntPtr.Zero, ref size);
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => UnitsInto(ref g, ref st, buf, ref len)
        );
    }

    private static uint UnitsInto(
        ref Guid subgroupId,
        ref Guid settingId,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadValueUnitsSpecifier(
            IntPtr.Zero,
            ref subgroupId,
            ref settingId,
            buffer,
            ref size
        );
    }

    private static string? ReadPossibleNameRaw(Guid subgroupId, Guid settingId, uint index)
    {
        var g = subgroupId;
        var st = settingId;
        uint size = 0;
        var probe = PowerReadPossibleFriendlyName(
            IntPtr.Zero,
            ref g,
            ref st,
            index,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => PossibleNameInto(ref g, ref st, index, buf, ref len)
        );
    }

    private static uint PossibleNameInto(
        ref Guid subgroupId,
        ref Guid settingId,
        uint index,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadPossibleFriendlyName(
            IntPtr.Zero,
            ref subgroupId,
            ref settingId,
            index,
            buffer,
            ref size
        );
    }

    private static string? ReadPossibleDescriptionRaw(Guid subgroupId, Guid settingId, uint index)
    {
        var g = subgroupId;
        var st = settingId;
        uint size = 0;
        var probe = PowerReadPossibleDescription(
            IntPtr.Zero,
            ref g,
            ref st,
            index,
            IntPtr.Zero,
            ref size
        );
        return ReadProbedString(
            probe,
            size,
            (buf, ref len) => PossibleDescriptionInto(ref g, ref st, index, buf, ref len)
        );
    }

    private static uint PossibleDescriptionInto(
        ref Guid subgroupId,
        ref Guid settingId,
        uint index,
        IntPtr buffer,
        ref uint size
    )
    {
        return PowerReadPossibleDescription(
            IntPtr.Zero,
            ref subgroupId,
            ref settingId,
            index,
            buffer,
            ref size
        );
    }

    private delegate uint FillBuffer(IntPtr buffer, ref uint size);

    private static string? ReadProbedString(uint probe, uint size, FillBuffer fill)
    {
        // Probe-then-read: NULL buffer returns the required size; some APIs
        // report ERROR_SUCCESS with size on probe, others ERROR_MORE_DATA.
        if (probe != ERROR_SUCCESS && probe != ERROR_MORE_DATA)
            return null;
        if (size == 0)
            return null;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal((int)size);
            if (fill(buffer, ref size) != ERROR_SUCCESS)
                return null;
            var text = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    private static IEnumerable<Guid> EnumerateGuids(uint access, Guid? schemeId, Guid? subgroupId)
    {
        for (uint index = 0; ; index++)
        {
            uint size = 16;
            var buffer = Marshal.AllocHGlobal(16);
            try
            {
                Guid sCopy = schemeId ?? Guid.Empty;
                Guid gCopy = subgroupId ?? Guid.Empty;
                IntPtr pScheme = schemeId.HasValue ? AddrOf(ref sCopy) : IntPtr.Zero;
                IntPtr pGroup = subgroupId.HasValue ? AddrOf(ref gCopy) : IntPtr.Zero;
                uint code;
                try
                {
                    code = PowerEnumerate(
                        IntPtr.Zero,
                        pScheme,
                        pGroup,
                        access,
                        index,
                        buffer,
                        ref size
                    );
                }
                finally
                {
                    if (pScheme != IntPtr.Zero)
                        Marshal.FreeHGlobal(pScheme);
                    if (pGroup != IntPtr.Zero)
                        Marshal.FreeHGlobal(pGroup);
                }
                if (code != ERROR_SUCCESS)
                    yield break;
                yield return Marshal.PtrToStructure<Guid>(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static IntPtr AddrOf(ref Guid value)
    {
        var ptr = Marshal.AllocHGlobal(16);
        Marshal.StructureToPtr(value, ptr, false);
        return ptr;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(
        IntPtr UserRootPowerKey,
        out IntPtr ActivePolicyGuid
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerEnumerate(
        IntPtr RootPowerKey,
        IntPtr SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        uint AccessFlags,
        uint Index,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadDescription(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadDescription(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadDescription(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerImportPowerScheme(
        IntPtr RootPowerKey,
        string ImportFileNamePath,
        IntPtr DestinationSchemeGuid
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerDuplicateScheme(
        IntPtr RootPowerKey,
        ref Guid SourceSchemeGuid,
        IntPtr DestinationSchemeGuid
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerDeleteScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerWriteFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerWriteDescription(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        uint BufferSize
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint AcValueIndex
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint DcValueIndex
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint AcValueIndex
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint DcValueIndex
    );

    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerIsSettingRangeDefined(
        ref Guid SubKeyGuid,
        ref Guid SettingGuid
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadValueMin(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint ValueMinimum
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadValueMax(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint ValueMaximum
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadValueIncrement(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint ValueIncrement
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadValueUnitsSpecifier(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadPossibleValue(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint Type,
        uint PossibleSettingIndex,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadPossibleFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint PossibleSettingIndex,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadPossibleDescription(
        IntPtr RootPowerKey,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint PossibleSettingIndex,
        IntPtr Buffer,
        ref uint BufferSize
    );

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
