using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public sealed class ThemeToGitHubIconConverter : ThemeConverterBase<BitmapImage>
{
    protected override BitmapImage ConvertTheme(ApplicationTheme theme, object parameter)
    {
        if (theme == ApplicationTheme.Dark)
            return new BitmapImage(
                new Uri("pack://application:,,,/Resources/Images/GitHubLogoWhite.png")
            );

        return new BitmapImage(
            new Uri("pack://application:,,,/Resources/Images/GitHubLogoBlack.png")
        );
    }
}
