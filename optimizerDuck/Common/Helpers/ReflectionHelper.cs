using System.Reflection;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Provides cached reflection discovery of implementations within application assemblies.
/// </summary>
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
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return [];
        }
    }

    private static readonly Dictionary<Type, List<Type>> _implementationCache = new();
    private static readonly Lock _cacheLock = new();

    /// <summary>
    ///     Finds all concrete class implementations of the specified interface type in loaded assemblies.
    /// </summary>
    /// <param name="interfaceType">The interface or base type to find implementations of.</param>
    /// <returns>A collection of matching non-abstract class <see cref="Type" /> instances.</returns>
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

    /// <summary>
    ///     Finds all concrete class implementations of <typeparamref name="TInterface" /> in loaded assemblies.
    /// </summary>
    /// <typeparam name="TInterface">The interface type to find implementations of.</typeparam>
    /// <returns>A collection of matching non-abstract class <see cref="Type" /> instances.</returns>
    public static IEnumerable<Type> FindImplementationsInLoadedAssemblies<TInterface>()
    {
        return FindImplementationsInLoadedAssemblies(typeof(TInterface));
    }
}
