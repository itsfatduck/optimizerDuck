using optimizerDuck.Domain.Execution;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     How the record presents one kind of recorded step: an icon and the resource key of its
///     label. The key is carried rather than the resolved text so the mapping stays testable
///     and the label follows the UI language.
/// </summary>
public sealed record ChangeKindDisplay
{
    public SymbolRegular Icon { get; init; }

    public string LabelKey { get; init; } = string.Empty;
}

/// <summary>
///     Maps a recorded kind of step to its presentation. An unrecognized kind falls back to the
///     appearance of a change, so a mistake is visible instead of hidden.
/// </summary>
public static class ChangeKindPresentation
{
    extension(ChangeKind kind)
    {
        /// <summary>Gets the icon and label key this kind of step is shown with.</summary>
        public ChangeKindDisplay ToDisplay()
        {
            return kind switch
            {
                ChangeKind.Skip => new ChangeKindDisplay
                {
                    Icon = SymbolRegular.CheckmarkCircle24,
                    LabelKey = "Optimizer.Details.Step.AlreadyCorrect",
                },
                ChangeKind.NotApplicable => new ChangeKindDisplay
                {
                    Icon = SymbolRegular.Info24,
                    LabelKey = "Optimizer.Details.Step.NotApplicable",
                },
                ChangeKind.Refused => new ChangeKindDisplay
                {
                    Icon = SymbolRegular.ShieldError24,
                    LabelKey = "Optimizer.Details.Step.Refused",
                },
                ChangeKind.Irreversible => new ChangeKindDisplay
                {
                    Icon = SymbolRegular.ArrowClockwise24,
                    LabelKey = "Optimizer.Details.Step.Irreversible",
                },
                _ => new ChangeKindDisplay
                {
                    Icon = SymbolRegular.Edit24,
                    LabelKey = "Optimizer.Details.Step.Changed",
                },
            };
        }
    }
}
