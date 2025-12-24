using System.ComponentModel;
using optimizerDuck.UI;

namespace optimizerDuck.Models;

public enum OptimizationImpact
{
    [Description($"[{Theme.Success}]Minimal Impact[/]")]
    Minimal,

    [Description($"[{Theme.Warning}]Moderate Impact[/]")]
    Moderate,

    [Description($"[{Theme.Error}]High Impact[/]")]
    Significant,

    [Description($"[{Theme.Error}]System-wide impact[/]")]
    Aggressive
}