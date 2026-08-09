using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

/// <summary>Returns the GitHub logo image that matches the current theme.</summary>
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
