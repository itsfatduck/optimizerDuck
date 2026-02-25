using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.StartupManager;

public partial class StartupTask : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;

    public required string TaskName { get; init; }
    public required string TaskPath { get; init; }
    public string? Description { get; init; }
}
