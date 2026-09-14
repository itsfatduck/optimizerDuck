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
        // The rows are built here, not in the provider: a failure list reads in the UI language,
        // and only falls back to the English text written for the log when a step carries nothing
        // else to show. The run the steps belong to decides whether they read as what was applied
        // or as what was put back.
        FailedSteps = new ObservableCollection<ChangeRecordStepViewModel>(
            failedSteps.Select(step => new ChangeRecordStepViewModel(step, operation))
        );
    }

    public ObservableCollection<ChangeRecordStepViewModel> FailedSteps { get; }

    public int FailedCount => FailedSteps.Count;
}
