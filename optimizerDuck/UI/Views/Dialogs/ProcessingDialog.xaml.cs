using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.UI.Views.Dialogs;

/// <summary>
///     Interaction logic for ProcessingOptimizationDialog.xaml
/// </summary>
public partial class ProcessingDialog : UserControl
{
    public ProcessingDialog()
    {
        InitializeComponent();
    }

    public ProcessingViewModel? ViewModel => DataContext as ProcessingViewModel;
}