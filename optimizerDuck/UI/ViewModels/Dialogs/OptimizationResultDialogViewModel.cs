using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public class OptimizationResultDialogViewModel : LocalizedObject
{
    public OptimizationResultDialogViewModel(IEnumerable<Change> failedSteps)
    {
        FailedSteps = new ObservableCollection<Change>(failedSteps);
    }

    public ObservableCollection<Change> FailedSteps { get; }

    public int FailedCount => FailedSteps.Count;
}
