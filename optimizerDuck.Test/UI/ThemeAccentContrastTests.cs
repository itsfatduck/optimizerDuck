using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Button = Wpf.Ui.Controls.Button;

namespace optimizerDuck.Test.UI;

public class ThemeAccentContrastTests
{
    [Fact]
    public Task SwitchingDarkToLight_UpdatesLivePrimaryButtonAndHyperlinkWithReadableContrast() =>
        RunInStaThreadAsync(() =>
        {
            // Touching Application's static members runs its static constructor, which registers
            // the pack:// web request prefix without creating an Application instance. Other
            // tests may do this first, so the test must not depend on run order.
            _ = Application.Current;
            _ = PackUriHelper.UriSchemePack;

            var themesDict = new ThemesDictionary { Theme = ApplicationTheme.Dark };
            var window = new Window();
            window.Resources.MergedDictionaries.Add(themesDict);
            window.Resources.MergedDictionaries.Add(new ControlsDictionary());

            var button = new Button { Appearance = ControlAppearance.Primary, Content = "Accept" };
            var hyperlink = new HyperlinkButton { Content = "Terms of Service" };
            var panel = new StackPanel();
            panel.Children.Add(button);
            panel.Children.Add(hyperlink);
            window.Content = panel;

            button.ApplyTemplate();
            hyperlink.ApplyTemplate();

            App.RefreshAccentBrushes(window.Resources, ApplicationTheme.Dark);

            Assert.Equal(
                Color.FromRgb(255, 247, 200),
                Assert.IsType<SolidColorBrush>(button.Background).Color
            );
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(button.Foreground).Color);
            Assert.Equal(
                Color.FromRgb(255, 247, 200),
                Assert.IsType<SolidColorBrush>(hyperlink.Foreground).Color
            );

            themesDict.Theme = ApplicationTheme.Light;
            App.RefreshAccentBrushes(window.Resources, ApplicationTheme.Light);

            var lightBg = Assert.IsType<SolidColorBrush>(button.Background).Color;
            var lightFg = Assert.IsType<SolidColorBrush>(button.Foreground).Color;
            var lightLink = Assert.IsType<SolidColorBrush>(hyperlink.Foreground).Color;

            Assert.Equal(Color.FromRgb(138, 94, 0), lightBg);
            Assert.Equal(Colors.White, lightFg);
            Assert.Equal(Color.FromRgb(122, 88, 0), lightLink);

            Assert.True(ContrastRatio(lightFg, lightBg) >= 4.5);
            Assert.True(ContrastRatio(lightLink, Colors.White) >= 4.5);

            themesDict.Theme = ApplicationTheme.Dark;
            App.RefreshAccentBrushes(window.Resources, ApplicationTheme.Dark);

            Assert.Equal(
                Color.FromRgb(255, 247, 200),
                Assert.IsType<SolidColorBrush>(button.Background).Color
            );
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(button.Foreground).Color);
            Assert.Null(Application.Current);

            window.Close();
            return Task.CompletedTask;
        });

    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher)
            );
            var frame = new DispatcherFrame();

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    frame.Continue = false;
                }
            });

            Dispatcher.PushFrame(frame);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var l1 = RelativeLuminance(foreground);
        var l2 = RelativeLuminance(background);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        static double Linearize(byte channel)
        {
            var s = channel / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);
    }
}
