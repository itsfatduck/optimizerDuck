using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public class OptimizationResultDialogViewModel : LocalizedObject
{
    public OptimizationResultDialogViewModel(
        IEnumerable<Change> failedSteps,
        ChangeRecordOperation operation = ChangeRecordOperation.Apply
    )
    {
        // The rows are built here so a failure list reads in the UI language, falling back to the
        // English log text only when a step carries nothing else to show. The run the steps
        // belong to decides whether they read as applied or as put back.
        FailedSteps = new ObservableCollection<ChangeRecordStepViewModel>(
            failedSteps.Select(step => new ChangeRecordStepViewModel(step, operation))
        );
    }

    public ObservableCollection<ChangeRecordStepViewModel> FailedSteps { get; }

    public int FailedCount => FailedSteps.Count;
}
