using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

public class ShellRevertStep : IRevertStep
{
    public ShellType ShellType { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Type => "Shell";

    public async Task<bool> ExecuteAsync()
    {
        return await Task.Run(() =>
        {
            var result = ShellType switch
            {
                ShellType.PowerShell => ShellService.PowerShell(Command),
                ShellType.CMD => ShellService.CMD(Command),
                _ => new ShellResult(Command, "", "Unknown shell type", 1, TimeSpan.Zero)
            };
            return result.ExitCode == 0;
        });
    }

    public JObject ToData()
    {
        return new JObject
        {
            [nameof(ShellType)] = ShellType.ToString(),
            [nameof(Command)] = Command
        };
    }

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

public enum ShellType
{
    PowerShell,
    CMD
}