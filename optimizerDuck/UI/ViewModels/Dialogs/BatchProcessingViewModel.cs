using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public partial class BatchProcessingViewModel : ObservableObject
{
    [ObservableProperty] private string? _currentOptimizationName;
    [ObservableProperty] private string? _subMessage;
    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isCompleted;

    public ObservableCollection<string> Errors { get; } = [];

    public BatchProcessingViewModel(int totalCount)
    {
        TotalCount = totalCount;
        SubProgressReporter = new Progress<ProcessingProgress>(p =>
        {
            SubMessage = p.Message;
        });
    }

    public IProgress<ProcessingProgress> SubProgressReporter { get; }

    public void Update(int index, string name, string subMessage)
    {
        CurrentIndex = index;
        CurrentOptimizationName = name;
        SubMessage = subMessage;
    }

    public void AddError(string optimizationName, string message)
    {
        Errors.Add($"{optimizationName}: {message}");
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
    }
}
