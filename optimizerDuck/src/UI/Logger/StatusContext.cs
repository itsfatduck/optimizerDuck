using Spectre.Console;

namespace optimizerDuck.UI.Logger;

public sealed class StatusContextScope : IDisposable
{
    private readonly StatusContext? _ctx;
    private readonly Spinner? _spinner;
    private readonly Style? _spinnerStyle;
    private readonly string? _status;

    public StatusContextScope(StatusContext? ctx, Spinner newSpinner, Style newStyle, string newStatus)
    {
        if (ctx is null)
        {
            _ctx = null;
            _spinner = null;
            _spinnerStyle = null;
            _status = null;
            return;
        }

        _ctx = ctx;
        _spinner = ctx.Spinner;
        _spinnerStyle = ctx.SpinnerStyle;
        _status = ctx.Status;

        // Apply temporary state
        ctx.Spinner(newSpinner);
        ctx.SpinnerStyle(newStyle);
        ctx.Status(newStatus);
    }

    public void Dispose()
    {
        if (_ctx is null)
            return;

        _ctx.Spinner(_spinner!);
        _ctx.SpinnerStyle = _spinnerStyle;
        _ctx.Status = _status!;
    }
}