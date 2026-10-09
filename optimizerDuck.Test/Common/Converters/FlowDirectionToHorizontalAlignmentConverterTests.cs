using System.Globalization;
using System.Windows;
using optimizerDuck.Common.Converters;

namespace optimizerDuck.Test.Common.Converters;

public class FlowDirectionToHorizontalAlignmentConverterTests
{
    private static readonly FlowDirectionToHorizontalAlignmentConverter ReadingStart = new();
    private static readonly FlowDirectionToHorizontalAlignmentConverter ReadingEnd = new()
    {
        ToEndEdge = true,
    };

    [Theory]
    [InlineData(FlowDirection.LeftToRight, HorizontalAlignment.Left)]
    [InlineData(FlowDirection.RightToLeft, HorizontalAlignment.Right)]
    public void Convert_ToStartEdge_HugsTheReadingEdge(
        FlowDirection direction,
        HorizontalAlignment expected
    )
    {
        var actual = ReadingStart.Convert(
            direction,
            typeof(HorizontalAlignment),
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, HorizontalAlignment.Right)]
    [InlineData(FlowDirection.RightToLeft, HorizontalAlignment.Left)]
    public void Convert_ToEndEdge_HugsTheFarEdge(
        FlowDirection direction,
        HorizontalAlignment expected
    )
    {
        var actual = ReadingEnd.Convert(
            direction,
            typeof(HorizontalAlignment),
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Convert_UnknownValue_FallsBackToLeftToRightLayout()
    {
        var actual = ReadingStart.Convert(
            "nonsense",
            typeof(HorizontalAlignment),
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal(HorizontalAlignment.Left, actual);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            ReadingStart.ConvertBack(
                HorizontalAlignment.Left,
                typeof(FlowDirection),
                null,
                CultureInfo.InvariantCulture
            )
        );
    }
}
