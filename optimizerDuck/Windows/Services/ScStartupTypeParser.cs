using System.Text.RegularExpressions;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Windows.Services;

/// <summary>
///     Shared parser for <c>sc.exe qc</c> START_TYPE output, used by
///     <c>ServiceProcessService</c>.
/// </summary>
public static partial class ScStartupTypeParser
{
    [GeneratedRegex(@"^\s*\S+\s*:\s*([0-4])\s(.+)$")]
    private static partial Regex StartTypeLineRegex();

    [GeneratedRegex(@"\([^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex DelayedRegex();

    public static ServiceStartupType? Parse(string stdout) => ParseWithMatch(stdout).Type;

    public static (ServiceStartupType? Type, bool Matched) ParseWithMatch(string stdout)
    {
        var lines = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = StartTypeLineRegex().Match(line);
            if (!match.Success)
                continue;

            var startValue = int.Parse(match.Groups[1].Value);
            var isDelayed = DelayedRegex().IsMatch(match.Groups[2].Value);

            return (
                startValue switch
                {
                    2 => isDelayed
                        ? ServiceStartupType.AutomaticDelayedStart
                        : ServiceStartupType.Automatic,
                    3 => ServiceStartupType.Manual,
                    4 => ServiceStartupType.Disabled,
                    _ => null,
                },
                true
            );
        }

        return (null, false);
    }
}
