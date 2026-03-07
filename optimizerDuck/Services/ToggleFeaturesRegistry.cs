using System.Collections.ObjectModel;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;

namespace optimizerDuck.Services;

public class ToggleFeaturesRegistry
{
    private static ToggleFeaturesRegistry? _instance;
    public static ToggleFeaturesRegistry Instance => _instance ??= new ToggleFeaturesRegistry();

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
}
