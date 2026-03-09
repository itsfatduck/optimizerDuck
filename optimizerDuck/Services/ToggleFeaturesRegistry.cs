using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class ToggleFeaturesRegistry
{
    public IToggleFeatureCategory[] Categories { get; private set; } = [];

    public void RegisterCategories()
    {
        var categories = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<IToggleFeatureCategory>()
            .Select(t =>
            {
                var featureTypes = t.GetNestedTypes(BindingFlags.Public)
                    .Where(nt => typeof(IToggleFeature).IsAssignableFrom(nt) && !nt.IsAbstract)
                    .Select(nt =>
                    {
                        var opt = (IToggleFeature)Activator.CreateInstance(nt)!;

                        if (opt is BaseToggleFeature bo)
                            bo.OwnerType = t;

                        return opt;
                    })
                    .ToList();

                if (featureTypes.Count == 0)
                    return null;

                var features = new ObservableCollection<IToggleFeature>(featureTypes);

                var instance = (IToggleFeatureCategory)Activator.CreateInstance(t)!;

                var featuresProp = t.GetProperty(nameof(IToggleFeatureCategory.Features),
                    BindingFlags.Public | BindingFlags.Instance);
                if (featuresProp != null && featuresProp.CanWrite)
                    featuresProp.SetValue(instance, features);

                return instance;
            })
            .Where(c => c != null)
            .Cast<IToggleFeatureCategory>()
            .OrderBy(c => c.Order)
            .ToArray();

        Categories = categories;
    }

    public IEnumerable<NavigationViewItem> GetNavigationItems()
    {
        if (Categories.Length == 0)
        {
            RegisterCategories();
        }

        return Categories.Select(c => new NavigationViewItem
        {
            Content = c.Name,
            TargetPageType = c.GetType().GetCustomAttribute<ToggleFeatureCategoryAttribute>()?.PageType,
            TargetPageTag = c.GetType().Name
        }).Where(item => item.TargetPageType != null);
    }
}