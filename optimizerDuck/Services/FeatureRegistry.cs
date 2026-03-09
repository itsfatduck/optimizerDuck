using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class FeatureRegistry
{
    public IFeatureCategory[] Categories { get; private set; } = [];

    public void RegisterCategories()
    {
        var categories = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<IFeatureCategory>()
            .Select(t =>
            {
                var featureTypes = t.GetNestedTypes(BindingFlags.Public)
                    .Where(nt => typeof(IFeature).IsAssignableFrom(nt) && !nt.IsAbstract)
                    .Select(nt =>
                    {
                        var opt = (IFeature)Activator.CreateInstance(nt)!;

                        if (opt is BaseFeature bo)
                            bo.OwnerType = t;

                        return opt;
                    })
                    .ToList();

                if (featureTypes.Count == 0)
                    return null;

                var features = new ObservableCollection<IFeature>(featureTypes);

                var instance = (IFeatureCategory)Activator.CreateInstance(t)!;

                var featuresProp = t.GetProperty(nameof(IFeatureCategory.Features),
                    BindingFlags.Public | BindingFlags.Instance);
                if (featuresProp != null && featuresProp.CanWrite)
                    featuresProp.SetValue(instance, features);

                return instance;
            })
            .Where(c => c != null)
            .Cast<IFeatureCategory>()
            .OrderBy(c => c.Order)
            .ToArray();

        Categories = categories;
    }

    public IEnumerable<NavigationViewItem> GetNavigationItems()
    {
        if (Categories.Length == 0) RegisterCategories();

        return Categories.Select(c => new NavigationViewItem
        {
            Content = c.Name,
            TargetPageType = c.GetType().GetCustomAttribute<FeatureCategoryAttribute>()?.PageType,
            TargetPageTag = c.GetType().Name
        }).Where(item => item.TargetPageType != null);
    }
}