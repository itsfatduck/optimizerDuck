using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace optimizerDuck.Common.Converters;

public sealed class ThemeToGitHubIconConverter : ThemeConverterBase<BitmapImage>
{
    private static readonly Lazy<BitmapImage> DarkIcon = new(() =>
        CreateFrozenIcon("pack://application:,,,/Resources/Images/GitHubLogoWhite.png")
    );

    private static readonly Lazy<BitmapImage> LightIcon = new(() =>
        CreateFrozenIcon("pack://application:,,,/Resources/Images/GitHubLogoBlack.png")
    );

    private static BitmapImage CreateFrozenIcon(string uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(uri);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    protected override BitmapImage ConvertTheme(ApplicationTheme theme, object parameter) =>
        theme == ApplicationTheme.Dark ? DarkIcon.Value : LightIcon.Value;
}
