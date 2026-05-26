using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using Wpf.Ui.Controls;

namespace optimizerDuck.Services;

public class CustomizeRegistry
{
    public ICustomizeCategory[] Categories { get; private set; } = [];

    public void RegisterCategories()
    {
        var categories = ReflectionHelper
            .FindImplementationsInLoadedAssemblies<ICustomizeCategory>()
            .Select(t =>
            {
                var featureTypes = t.GetNestedTypes(BindingFlags.Public)
                    .Where(nt => typeof(ICustomizeSetting).IsAssignableFrom(nt) && !nt.IsAbstract)
                    .Select(nt =>
                    {
                        var opt = (ICustomizeSetting)Activator.CreateInstance(nt)!;

                        if (opt is BaseCustomizeSetting bo)
                            bo.OwnerType = t;

                        return opt;
                    })
                    .ToList();

                if (featureTypes.Count == 0)
                    return null;

                var features = new ObservableCollection<ICustomizeSetting>(featureTypes);

                var instance = (ICustomizeCategory)Activator.CreateInstance(t)!;

                var featuresProp = t.GetProperty(
                    nameof(ICustomizeCategory.Features),
                    BindingFlags.Public | BindingFlags.Instance
                );
                if (featuresProp != null && featuresProp.CanWrite)
                    featuresProp.SetValue(instance, features);

                return instance;
            })
            .Where(c => c != null)
            .Cast<ICustomizeCategory>()
            .OrderBy(c => c.Order)
            .ToArray();

        Categories = categories;
    }

    public IEnumerable<NavigationViewItem> GetNavigationItems()
    {
        if (Categories.Length == 0)
            RegisterCategories();

        return Categories
            .Select(c => new NavigationViewItem
            {
                Content = c.Name,
                TargetPageType = c.GetType()
                    .GetCustomAttribute<CustomizeCategoryAttribute>()
                    ?.PageType,
                TargetPageTag = c.GetType().Name,
            })
            .Where(item => item.TargetPageType != null);
    }
}
