using System.Reflection;

namespace optimizerDuck.Common.Helpers;

public static class ReflectionHelper
{
    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // return only the types that were loaded successfully
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return [];
        }
    }

    private static readonly Dictionary<Type, List<Type>> _implementationCache = new();
    private static readonly object _cacheLock = new();

    public static IEnumerable<Type> FindImplementationsInLoadedAssemblies(Type interfaceType)
    {
        lock (_cacheLock)
        {
            if (_implementationCache.TryGetValue(interfaceType, out var cached))
                return cached;

            var types = AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(a =>
                    a.FullName != null
                    && a.FullName.StartsWith(
                        nameof(optimizerDuck),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .SelectMany(SafeGetTypes)
                .Where(t =>
                    t != interfaceType
                    && t is { IsClass: true, IsAbstract: false }
                    && interfaceType.IsAssignableFrom(t)
                )
                .ToList();

            _implementationCache[interfaceType] = types;
            return types;
        }
    }

    public static IEnumerable<Type> FindImplementationsInLoadedAssemblies<TInterface>()
    {
        return FindImplementationsInLoadedAssemblies(typeof(TInterface));
    }
}
