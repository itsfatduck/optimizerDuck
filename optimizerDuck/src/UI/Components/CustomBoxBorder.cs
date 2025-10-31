using Spectre.Console;
using Spectre.Console.Rendering;

namespace optimizerDuck.UI.Components;

public static class CustomBoxBorder
{
    public static BoxBorder UnderlineBorder => new CUnderlineBorder();
    public static BoxBorder LeftBorder => new CLeftBorder();
    public static BoxBorder EmptyBorder => new CEmptyBorder();

    private sealed class CUnderlineBorder : BoxBorder
    {
        public override string GetPart(BoxBorderPart part)
        {
            return part switch
            {
                BoxBorderPart.TopLeft => " ",
                BoxBorderPart.Top => " ",
                BoxBorderPart.TopRight => " ",
                BoxBorderPart.Left => " ",
                BoxBorderPart.Right => " ",
                BoxBorderPart.BottomLeft => "─",
                BoxBorderPart.Bottom => "─",
                BoxBorderPart.BottomRight => "─",
                _ => throw new InvalidOperationException("Unknown border part.")
            };
        }
    }

    private sealed class CLeftBorder : BoxBorder
    {
        public override string GetPart(BoxBorderPart part)
        {
            return part switch
            {
                BoxBorderPart.TopLeft => " ",
                BoxBorderPart.Top => " ",
                BoxBorderPart.TopRight => " ",
                BoxBorderPart.Left => "│",
                BoxBorderPart.Right => " ",
                BoxBorderPart.BottomLeft => " ",
                BoxBorderPart.Bottom => " ",
                BoxBorderPart.BottomRight => " ",
                _ => throw new InvalidOperationException("Unknown border part.")
            };
        }
    }

    private sealed class CEmptyBorder : BoxBorder
    {
        public override string GetPart(BoxBorderPart part)
        {
            return part switch
            {
                BoxBorderPart.TopLeft => " ",
                BoxBorderPart.Top => " ",
                BoxBorderPart.TopRight => " ",
                BoxBorderPart.Left => " ",
                BoxBorderPart.Right => " ",
                BoxBorderPart.BottomLeft => " ",
                BoxBorderPart.Bottom => " ",
                BoxBorderPart.BottomRight => " ",
                _ => throw new InvalidOperationException("Unknown border part.")
            };
        }
    }
}