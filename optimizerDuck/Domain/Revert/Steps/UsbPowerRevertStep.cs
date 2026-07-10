using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Optimization.Providers;

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
    public string Description => Translations.Revert_UsbPower_Description;

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync()
    {
        if (States.Count == 0)
            return true;

        var payload = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(States))
        );

        var script =
            "$json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"
            + payload
            + "')); "
            + "$states = $json | ConvertFrom-Json; "
            + "foreach ($s in $states) { "
            + "$obj = Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | "
            + "Where-Object { $_.InstanceName -eq $s.InstanceName }; "
            + "if ($obj -and $obj.Enable -ne [bool]$s.Enable) { "
            + "Set-CimInstance -CimInstance $obj -Property @{ Enable = [bool]$s.Enable } | Out-Null "
            + "}}";

        var result = await ShellService.PowerShellAsync(script);

        if (result.ExitCode != 0)
        {
            var error = !string.IsNullOrWhiteSpace(result.Stderr)
                ? result.Stderr
                : string.Format(Translations.Revert_UsbPower_Error_CommandFailed, result.ExitCode);
            throw new StepExecutionException(error, result.Stderr);
        }

        return true;
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
