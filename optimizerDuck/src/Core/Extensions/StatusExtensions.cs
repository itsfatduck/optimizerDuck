using optimizerDuck.UI.Logger;
using Spectre.Console;

namespace optimizerDuck.Core.Extensions;

public static class StatusExtensions
{
    public static void StartGlobal(this Status status,
        string message,
        Action<StatusContext> action)
    {
        var task = status.StartAsync(message, ctx =>
        {
            GlobalStatus.Current = ctx;
            action(ctx);
            return Task.CompletedTask;
        });

        task.GetAwaiter().GetResult();
    }

    public static T StartGlobal<T>(
        this Status status,
        string message,
        Func<StatusContext, T> func)
    {
        return status.Start(message, ctx =>
        {
            GlobalStatus.Current = ctx;
            return func(ctx);
        });
    }

    public static async Task StartAsyncGlobal(
        this Status status,
        string message,
        Func<StatusContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(status);

        _ = await status.StartAsync<object?>(message, async ctx =>
        {
            GlobalStatus.Current = ctx;
            await action(ctx).ConfigureAwait(false);
            return null;
        }).ConfigureAwait(false);
    }

    public static async Task<T> StartAsyncGlobal<T>(
        this Status status,
        string message,
        Func<StatusContext, Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(func);

        return await status.StartAsync<T>(message, async ctx =>
        {
            GlobalStatus.Current = ctx;
            return await func(ctx).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}