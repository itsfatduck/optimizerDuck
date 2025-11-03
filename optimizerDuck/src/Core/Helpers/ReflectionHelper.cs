using System.Reflection;
using optimizerDuck.Core.Managers;

namespace optimizerDuck.Core.Helpers;

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

    public static IEnumerable<Type> FindImplementationsInLoadedAssemblies(Type interfaceType)
    {
        if (!interfaceType.IsInterface)
            throw new ArgumentException("interfaceType must be an interface", nameof(interfaceType));

        return CacheManager.GetOrCreate(interfaceType.FullName!, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => t != interfaceType && t is { IsClass: true, IsAbstract: false } && interfaceType.IsAssignableFrom(t))
                .ToList();
        });
    }

    public static IEnumerable<Type> FindImplementationsInLoadedAssemblies<TInterface>()
    {
        return FindImplementationsInLoadedAssemblies(typeof(TInterface));
    }
}