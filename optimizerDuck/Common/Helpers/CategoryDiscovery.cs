using System.Reflection;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Reflection-based discovery of category classes and the items nested inside them.
///     Used by <c>OptimizationRegistry</c> and <c>CustomizeRegistry</c> so no manual
///     registration array has to be maintained when a category or item is added.
/// </summary>
public static class CategoryDiscovery
{
    /// <summary>
    ///     Builds one instance per discovered category type, populating the collection
    ///     property named <paramref name="itemsPropertyName"/> with its nested items.
    ///     Categories that contain no items are skipped.
    /// </summary>
    /// <param name="itemsPropertyName">The public collection property to fill (e.g. <c>Optimizations</c>).</param>
    /// <param name="buildItems">Builds the nested items for a category type.</param>
    /// <param name="orderBy">Sort key applied to the resulting categories.</param>
    public static TCategory[] Discover<TCategory, TItem>(
        string itemsPropertyName,
        Func<Type, List<TItem>> buildItems,
        Func<TCategory, int> orderBy
    )
        where TCategory : class
    {
        ArgumentNullException.ThrowIfNull(itemsPropertyName);
        ArgumentNullException.ThrowIfNull(buildItems);
        ArgumentNullException.ThrowIfNull(orderBy);

        var categories = new List<TCategory>();

        foreach (
            var categoryType in ReflectionHelper.FindImplementationsInLoadedAssemblies<TCategory>()
        )
        {
            var items = buildItems(categoryType);
            if (items.Count == 0)
                continue;

            var instance = (TCategory)Activator.CreateInstance(categoryType)!;
            PopulateItems(instance, categoryType, itemsPropertyName, items);
            categories.Add(instance);
        }

        return categories.OrderBy(orderBy).ToArray();
    }

    /// <summary>
    ///     Instantiates every public nested class assignable to <typeparamref name="TItem"/>.
    /// </summary>
    /// <param name="category">The category type whose nested types are scanned.</param>
    /// <param name="init">Optional callback invoked with each item and its owning category.</param>
    public static List<TItem> NestedItems<TItem>(Type category, Action<TItem, Type>? init = null)
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(category);

        var items = new List<TItem>();

        foreach (var nested in category.GetNestedTypes(BindingFlags.Public))
        {
            if (!typeof(TItem).IsAssignableFrom(nested) || !nested.IsClass || nested.IsAbstract)
                continue;

            var item = (TItem)Activator.CreateInstance(nested)!;
            init?.Invoke(item, category);
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    ///     Assigns the built items to the category's collection property, adapting a
    ///     <see cref="List{T}"/> to whatever collection type the property declares.
    /// </summary>
    private static void PopulateItems<TCategory, TItem>(
        TCategory instance,
        Type categoryType,
        string itemsPropertyName,
        List<TItem> items
    )
        where TCategory : class
    {
        var property = categoryType.GetProperty(
            itemsPropertyName,
            BindingFlags.Public | BindingFlags.Instance
        );

        if (property is not { CanWrite: true })
        {
            throw new InvalidOperationException(
                $"Category '{categoryType.FullName}' must expose a writable '{itemsPropertyName}' collection."
            );
        }

        // List<T> matches the property type directly; otherwise build the declared
        // collection from the same items (e.g. ObservableCollection<T>).
        var value = property.PropertyType.IsAssignableFrom(items.GetType())
            ? items
            : Activator.CreateInstance(property.PropertyType, items);

        property.SetValue(instance, value);
    }
}
