using System.Globalization;
using System.Windows;
using optimizerDuck.Common.Converters;

namespace optimizerDuck.Test.Common.Converters;

public class ValueConditionToVisibilityConverterTests
{
    private static readonly ValueConditionToVisibilityConverter NotEmpty = new()
    {
        ConditionType = ValueConditionType.IsNotNullOrEmpty,
    };

    [Theory]
    [InlineData(null, Visibility.Collapsed)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData("   ", Visibility.Collapsed)]
    [InlineData(@"C:\Windows\Temp", Visibility.Visible)]
    public void Convert_NotNullOrEmpty_ReportsVisibility(string? value, Visibility expected)
    {
        var actual = NotEmpty.Convert(
            value,
            typeof(Visibility),
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal(expected, actual);
    }
}
