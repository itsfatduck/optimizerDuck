using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui.TaskBar;

namespace optimizerDuck.UI.Dialogs;

/// <summary>
///     Interaction logic for ProcessingOptimizationDialog.xaml
/// </summary>
public partial class ProcessingDialog : UserControl
{
    private Window? _trackedWindow;

    public ProcessingDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ApplyToTaskbar();
        Unloaded += (_, _) => ClearTaskbar();
    }

    /// <summary>Maps dialog progress to a taskbar progress indicator state.</summary>
    internal static (TaskBarProgressState State, int Current, int Total) MapProgress(
        bool isIndeterminate,
        int value,
        int total
    )
    {
        if (isIndeterminate || total <= 0)
            return (TaskBarProgressState.Indeterminate, 0, 0);

        return (TaskBarProgressState.Normal, Math.Clamp(value, 0, total), total);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldViewModel)
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is INotifyPropertyChanged newViewModel)
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyToTaskbar();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(ProcessingViewModel.IsIndeterminate)
                or nameof(ProcessingViewModel.Value)
                or nameof(ProcessingViewModel.Total)
        )
        {
            ApplyToTaskbar();
        }
    }

    private void ApplyToTaskbar()
    {
        if (DataContext is not ProcessingViewModel viewModel)
            return;

        var window = _trackedWindow ??= Window.GetWindow(this);
        if (window is null)
            return;

        var dispatcher = window.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ApplyToTaskbar);
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var (state, current, total) = MapProgress(
            viewModel.IsIndeterminate,
            viewModel.Value,
            viewModel.Total
        );

        if (state == TaskBarProgressState.Normal)
            _ = TaskBarProgress.SetValue(handle, state, current, total);
        else
            _ = TaskBarProgress.SetState(handle, state);
    }

    private void ClearTaskbar()
    {
        var window = _trackedWindow;
        if (window is null)
            return;

        var dispatcher = window.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ClearTaskbar);
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        _ = TaskBarProgress.SetState(handle, TaskBarProgressState.None);
    }
}
