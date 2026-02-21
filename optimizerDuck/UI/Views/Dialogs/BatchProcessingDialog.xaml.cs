using System.Windows.Controls;
using optimizerDuck.UI.ViewModels.Dialogs;

namespace optimizerDuck.UI.Views.Dialogs;

public partial class BatchProcessingDialog : UserControl
{
    public BatchProcessingDialog()
    {
        InitializeComponent();
    }

    public BatchProcessingViewModel? ViewModel => DataContext as BatchProcessingViewModel;
}
