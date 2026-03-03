using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace optimizerDuck.Core.Models.StartupManager;

public partial class StartupTask : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private ImageSource? _logoImage;

    public required string TaskName { get; init; }
    public required string TaskPath { get; init; }
    public string? Description { get; init; }
    public string? TriggerSummary { get; init; }
    public string? ActionSummary { get; init; }
    public bool IsMicrosoftTask => TaskPath.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
}