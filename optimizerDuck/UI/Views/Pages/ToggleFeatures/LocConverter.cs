using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.UI.Views.Pages.ToggleFeatures;

public class LocConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key)
        {
            return Loc.Instance[key];
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
