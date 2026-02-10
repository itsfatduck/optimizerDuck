using System.Windows;

namespace optimizerDuck.Common.Helpers;

public static class ThemeResource
{
    public static T Get<T>(string key) where T : class
    {
        return Application.Current.Resources[key] as T
               ?? throw new KeyNotFoundException(key);
    }
}