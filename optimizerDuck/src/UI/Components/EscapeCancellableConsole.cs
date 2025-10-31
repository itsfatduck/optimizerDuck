using Spectre.Console;
using Spectre.Console.Rendering;

namespace optimizerDuck.UI.Components;

// original from https://github.com/spectreconsole/spectre.console/discussions/700#discussioncomment-12960984
internal sealed class EscapeCancellableConsole : IAnsiConsole, IDisposable
{
    private readonly IAnsiConsole _console;
    private readonly CancellationTokenSource _cts = new();
    private readonly EscapeCancellableInput _input;

    public EscapeCancellableConsole(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _input = new EscapeCancellableInput(console.Input, _cts);

        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public Profile Profile => _console.Profile;
    public IAnsiConsoleCursor Cursor => _console.Cursor;
    public IAnsiConsoleInput Input => _input;
    public IExclusivityMode ExclusivityMode => _console.ExclusivityMode;
    public RenderPipeline Pipeline => _console.Pipeline;

    public void Clear(bool home)
    {
        _console.Clear(home);
    }

    public void Write(IRenderable renderable)
    {
        _console.Write(renderable);
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _cts.Dispose();
    }


    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    public Task<T> PromptAsync<T>(IPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        return AnsiConsoleExtensions.PromptAsync(this, prompt, MergeToken(cancellationToken));
    }

    private CancellationToken MergeToken(CancellationToken external)
    {
        if (external == CancellationToken.None || external == _cts.Token)
            return _cts.Token;

        var linked = CancellationTokenSource.CreateLinkedTokenSource(external, _cts.Token);
        return linked.Token;
    }
}

internal sealed class EscapeCancellableInput(IAnsiConsoleInput original, CancellationTokenSource cts)
    : IAnsiConsoleInput
{
    private readonly CancellationTokenSource _cts = cts ?? throw new ArgumentNullException(nameof(cts));
    private readonly IAnsiConsoleInput _original = original ?? throw new ArgumentNullException(nameof(original));

    public bool IsKeyAvailable()
    {
        return _original.IsKeyAvailable();
    }

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        var key = _original.ReadKey(intercept);
        if (key?.Key == ConsoleKey.Escape)
            _cts.Cancel();

        return key;
    }

    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        var key = await _original.ReadKeyAsync(intercept, cancellationToken).ConfigureAwait(false);
        if (key?.Key == ConsoleKey.Escape)
            await _cts.CancelAsync();

        return key;
    }
}