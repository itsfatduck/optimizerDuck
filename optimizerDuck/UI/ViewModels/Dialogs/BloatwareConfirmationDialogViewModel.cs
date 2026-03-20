using optimizerDuck.Domain.Optimizations.Models.Bloatware;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public sealed class BloatwareConfirmationDialogViewModel
{
    public BloatwareConfirmationDialogViewModel(IEnumerable<AppXPackage> selectedPackages)
    {
        Items = selectedPackages
            .Select((package, index) => new BloatwareConfirmationItemViewModel(index + 1, package))
            .ToList();
    }

    public IReadOnlyList<BloatwareConfirmationItemViewModel> Items { get; }
}

public sealed class BloatwareConfirmationItemViewModel(int index, AppXPackage package)
{
    public int Index { get; } = index;

    public AppXPackage Package { get; } = package;
}
