using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public partial class ProcessingViewModel : ObservableObject
{
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _value;

    public ProcessingViewModel()
    {
        ProgressReporter = new Progress<ProcessingProgress>(p =>
        {
            IsIndeterminate = p.IsIndeterminate;
            Message = p.Message;
            Value = p.Value;
            Total = p.Total;
        });
    }

    public IProgress<ProcessingProgress> ProgressReporter { get; }
}