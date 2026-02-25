using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public sealed class ThemeToGitHubIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ApplicationTheme theme && theme == ApplicationTheme.Dark)
            return new BitmapImage(
                new Uri("pack://application:,,,/Resources/Images/GitHubLogoWhite.png"));

        return new BitmapImage(
            new Uri("pack://application:,,,/Resources/Images/GitHubLogoBlack.png"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}