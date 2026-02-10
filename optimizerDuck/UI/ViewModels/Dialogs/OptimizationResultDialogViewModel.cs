using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Models.Optimization;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public class OptimizationResultDialogViewModel : ObservableObject
{
    public OptimizationResultDialogViewModel(IEnumerable<OperationStepResult> failedSteps)
    {
        FailedSteps = new ObservableCollection<OperationStepResult>(failedSteps);
    }

    public ObservableCollection<OperationStepResult> FailedSteps { get; }

    public int FailedCount => FailedSteps.Count;
}