using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public sealed class ScheduledTaskDeleteDialogViewModel(ScheduledTaskModel task)
{
    public ScheduledTaskModel Task { get; } = task;
}
