using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
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
                    .ToList();

                var features = new ObservableCollection<IToggleFeature>(
                    featureTypes
                        .Select(nt => Activator.CreateInstance(nt)!)
                        .Cast<IToggleFeature>()
                        .ToList()
                );

                if (features.Count == 0)
                    return null;

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

    public IToggleFeatureCategory? GetCategory(Type type)
    {
        return Categories.FirstOrDefault(c => c.GetType() == type);
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