using optimizerDuck.Core.Extensions;
using Spectre.Console;

namespace optimizerDuck.UI.Components;

public class PromptPanel
{
    private readonly BoxBorder _border = BoxBorder.Rounded;
    private readonly string _color = "white";
    private readonly string _description;
    private readonly char _index;
    private readonly string _title;
    private readonly int? _width;

    public PromptPanel(char index, string title, string description, string color, BoxBorder? border = null,
        int? width = null)
    {
        _index = index;
        _title = title;
        _description = description;
        _color = color;
        _width = width ?? _width;
        _border = border ?? _border;
    }

    public Panel Build()
    {
        var desc = _width.HasValue
            ? _description.LimitWidth(_width.Value)
            : _description;

        var panel = new Panel(
            $"[black on {_color}] {_index} [/] " +
            $"[bold {_color}]{_title}[/]\n[{Theme.Muted}]{desc}[/]"
        )
        {
            Padding = new Padding(2, 0),
            Border = _border,
            BorderStyle = _color,
            Expand = false
        };

        if (_width.HasValue)
            panel.Width = _width.Value;

        return panel;
    }
}