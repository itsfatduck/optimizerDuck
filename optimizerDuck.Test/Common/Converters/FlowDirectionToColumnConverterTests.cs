using System.Globalization;
using System.Windows;
using optimizerDuck.Common.Converters;

namespace optimizerDuck.Test.Common.Converters;

public class FlowDirectionToColumnConverterTests
{
    private static readonly FlowDirectionToColumnConverter ReadingStart = new();
    private static readonly FlowDirectionToColumnConverter ReadingEnd = new() { Invert = true };

    [Theory]
    [InlineData(FlowDirection.LeftToRight, 0)]
    [InlineData(FlowDirection.RightToLeft, 1)]
    public void Convert_NotInverted_StartsTheRowAtTheReadingEdge(
        FlowDirection direction,
        int expected
    )
    {
        var actual = ReadingStart.Convert(
            direction,
            typeof(int),
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, 1)]
    [InlineData(FlowDirection.RightToLeft, 0)]
    public void Convert_Inverted_ReturnsTheComplementaryColumn(
        FlowDirection direction,
        int expected
    )
    {
        var actual = ReadingEnd.Convert(direction, typeof(int), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Convert_PairedConverters_NeverShareAColumn()
    {
        foreach (var direction in new[] { FlowDirection.LeftToRight, FlowDirection.RightToLeft })
        {
            var start = ReadingStart.Convert(
                direction,
                typeof(int),
                null,
                CultureInfo.InvariantCulture
            );
            var end = ReadingEnd.Convert(
                direction,
                typeof(int),
                null,
                CultureInfo.InvariantCulture
            );

            Assert.NotEqual(start, end);
        }
    }

    [Fact]
    public void Convert_UnknownValue_FallsBackToLeftToRightLayout()
    {
        var actual = ReadingStart.Convert(null, typeof(int), null, CultureInfo.InvariantCulture);

        Assert.Equal(0, actual);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            ReadingStart.ConvertBack(0, typeof(FlowDirection), null, CultureInfo.InvariantCulture)
        );
    }
}
