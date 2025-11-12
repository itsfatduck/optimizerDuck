using optimizerDuck.UI;
using System.ComponentModel;

namespace optimizerDuck.Models;

public enum OptimizationImpact
{
    [Description($"[{Theme.Success}]Low[/]")]
    Low,
    [Description($"[{Theme.Warning}]Medium[/]")]
    Medium,
    [Description($"[{Theme.Error}]High[/]")]
    High,
    [Description($"[{Theme.Error}]Critical[/]")]
    Critical
}
