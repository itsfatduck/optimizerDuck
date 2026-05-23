using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Restores USB <c>MSPower_DeviceEnable</c> states captured before disable.
/// </summary>
public class UsbPowerRevertStep : IRevertStep
{
    /// <summary>
    ///     A single device instance and its original enabled flag.
    /// </summary>
    public sealed class DeviceState
    {
        public string InstanceName { get; set; } = string.Empty;
        public bool Enable { get; set; }
    }

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
        return result.ExitCode == 0;
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
        var states =
            data[nameof(States)]?.ToObject<List<DeviceState>>()
            ?? data.ToObject<List<DeviceState>>()
            ?? [];

        return new UsbPowerRevertStep { States = states };
    }
}
