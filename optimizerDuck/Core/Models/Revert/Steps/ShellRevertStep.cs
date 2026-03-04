using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

/// <summary>
///     Represents a revert step that re-executes a shell command to undo changes.
/// </summary>
public class ShellRevertStep : IRevertStep
{
    /// <summary>
    ///     The type of shell to use for execution.
    /// </summary>
    public ShellType ShellType { get; set; }

    /// <summary>
    ///     The command string to execute for reverting.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Type => "Shell";

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync()
    {
        return await Task.Run(() =>
        {
            var result = ShellType switch
            {
                ShellType.PowerShell => ShellService.PowerShell(Command),
                ShellType.CMD => ShellService.CMD(Command),
                _ => new ShellResult
                {
                    Command = Command, Stdout = "", Stderr = "Unknown shell type", ExitCode = 1,
                    Duration = TimeSpan.Zero
                }
            };
            return result.ExitCode == 0;
        });
    }

    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(ShellType)] = ShellType.ToString(),
            [nameof(Command)] = Command
        };
    }

    /// <summary>
    ///     Deserializes a <see cref="ShellRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="ShellRevertStep" /> instance.</returns>
    public static ShellRevertStep FromData(JToken data)
    {
        return new ShellRevertStep
        {
            ShellType = Enum.Parse<ShellType>(
                data[nameof(ShellType)]?.ToString() ?? "PowerShell"),
            Command = data[nameof(Command)]?.ToString() ?? string.Empty
        };
    }
}

/// <summary>
///     Specifies the type of shell used for command execution.
/// </summary>
public enum ShellType
{
    /// <summary>Windows PowerShell.</summary>
    PowerShell,

    /// <summary>Command Prompt (cmd.exe).</summary>
    CMD
}