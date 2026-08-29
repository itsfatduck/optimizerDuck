using System.Windows;

namespace optimizerDuck.Common.Helpers;

public static class ThemeResource
{
    public static T? Get<T>(string key)
        where T : class
    {
        if (Application.Current is null)
            return null;

        return Application.Current.TryFindResource(key) as T;
    }
}
