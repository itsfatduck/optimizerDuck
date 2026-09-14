using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Restores USB <c>MSPower_DeviceEnable</c> states captured before disable.
/// </summary>
public class UsbPowerRevertStep : IRevertStep
{
    /// <summary>
    ///     Represents a single USB device instance and its original <c>MSPower_DeviceEnable</c> state.
    /// </summary>
    public sealed class DeviceState
    {
        /// <summary>
        ///     Gets or sets the WMI <c>InstanceName</c> of the USB device.
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets a value that indicates whether the device was originally enabled.
        /// </summary>
        public bool Enable { get; set; }
    }

    /// <summary>
    ///     Gets or sets the list of USB device states to restore.
    /// </summary>
    public IList<DeviceState> States { get; set; } = [];

    /// <inheritdoc />
    public string Type => "UsbPower";

    /// <inheritdoc />
    public string Description => Loc.Instance["Revert.UsbPower.Description"];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        if (States.Count == 0)
            return Task.FromResult(true);

        var captured = States
            .Select(static state => new UsbPowerService.UsbPowerState(
                state.InstanceName,
                state.Enable
            ))
            .ToList();

        var result = UsbPowerService.Restore(captured);

        if (result is null || result.FailedDevices.Count > 0)
        {
            var detail = result is null ? string.Empty : string.Join(", ", result.FailedDevices);
            logger.LogWarning(
                "[USB][REVERT][FAIL] WMI restore refused or unavailable for {Detail}",
                detail
            );
            throw new StepExecutionException(
                Loc.Instance["Revert.UsbPower.Error.RestoreFailed"],
                detail
            );
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject { [nameof(States)] = JArray.FromObject(States) };
    }

    /// <summary>
    ///     Deserializes a <see cref="UsbPowerRevertStep" /> from JSON data.
    /// </summary>
    public static UsbPowerRevertStep FromData(JObject data)
    {
        var states = data[nameof(States)]?.ToObject<List<DeviceState>>() ?? [];

        return new UsbPowerRevertStep { States = states };
    }
}
