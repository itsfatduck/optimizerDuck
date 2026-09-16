using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui.Controls;
using Wpf.Ui.TaskBar;

namespace optimizerDuck.UI.Dialogs;

public partial class ProcessingDialog : UserControl
{
    private Window? _trackedWindow;
    private ContentDialog? _hostDialog;

    /// <summary>True once the run is over, so a late report cannot put the bar back.</summary>
    private bool _detached;

    public ProcessingDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hiding a ContentDialog does not reliably unload its content, so drop the bar when the
        // host dialog closes rather than waiting for Unloaded.
        _detached = false;
        _hostDialog = FindHostDialog(this);
        if (_hostDialog is not null)
            _hostDialog.Closed += OnHostDialogClosed;

        ApplyToTaskbar();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
    }

    private void OnHostDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        Detach();
    }

    /// <summary>Clears the bar and stops listening for progress reports.</summary>
    private void Detach()
    {
        _detached = true;
        if (_hostDialog is not null)
        {
            _hostDialog.Closed -= OnHostDialogClosed;
            _hostDialog = null;
        }

        ClearTaskbar();
    }

    private static ContentDialog? FindHostDialog(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is ContentDialog dialog)
                return dialog;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : null;
        }

        return null;
    }

    /// <summary>
    ///     Maps dialog progress to a taskbar indicator state. A run that reached its total maps to
    ///     <see cref="TaskBarProgressState.None" />, so a finished bar cannot stay behind.
    /// </summary>
    internal static (TaskBarProgressState State, int Current, int Total) MapProgress(
        bool isIndeterminate,
        int value,
        int total
    )
    {
        if (isIndeterminate || total <= 0)
            return (TaskBarProgressState.Indeterminate, 0, 0);

        var current = Math.Clamp(value, 0, total);
        return current >= total
            ? (TaskBarProgressState.None, 0, 0)
            : (TaskBarProgressState.Normal, current, total);
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
        // The window handle stays cached after the dialog is gone, so a report landing once the
        // run finished would turn the bar back on with nothing left to clear it.
        if (_detached || !IsLoaded)
            return;

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
