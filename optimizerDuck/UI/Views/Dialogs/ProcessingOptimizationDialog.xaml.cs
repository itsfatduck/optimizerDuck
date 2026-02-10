using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.UI.Views.Dialogs;

/// <summary>
///     Interaction logic for ProcessingOptimizationDialog.xaml
/// </summary>
public partial class ProcessingOptimizationDialog : UserControl
{
    public ProcessingOptimizationDialog()
    {
        InitializeComponent();
    }

    public ProcessingOptimizationViewModel? ViewModel => DataContext as ProcessingOptimizationViewModel;
}