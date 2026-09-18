using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Reports an unhandled failure to the user once per failure, and never throws.
/// </summary>
public sealed class UserErrorSurface
{
    private readonly ILogger<UserErrorSurface>? _logger;
    private readonly Action<string, string>? _present;

    // Key and value are the same failure: the table only remembers which ones were already
    // reported, and it holds them weakly, so a reported failure is never kept alive by it.
    private readonly ConditionalWeakTable<Exception, Exception> _reported = new();
    private readonly ISnackbarService? _snackbar;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserErrorSurface" /> class that reports
    ///     through the shell's snackbar.
    /// </summary>
    /// <param name="snackbar">The snackbar service of the shell.</param>
    /// <param name="logger">The logger a failed report is written to.</param>
    public UserErrorSurface(ISnackbarService snackbar, ILogger<UserErrorSurface> logger)
    {
        _snackbar = snackbar;
        _logger = logger;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserErrorSurface" /> class that hands the
    ///     report to <paramref name="present" />.
    /// </summary>
    /// <param name="present">Receives the report's title and message.</param>
    internal UserErrorSurface(Action<string, string> present)
    {
        _present = present;
    }

    /// <summary>Reports a failure to the user, once per exception.</summary>
    /// <param name="exception">The failure to report.</param>
    /// <returns>
    ///     <see langword="true" /> if this call reported the failure; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public bool TryReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!_reported.TryAdd(exception, exception))
            return false;

        var title = Loc.Instance["Error.Unhandled.Title"];
        var message =
            Loc.Instance["Error.Unhandled.Message", exception.Message]
            + Environment.NewLine
            + Loc.Instance["Error.Unhandled.LogHint"];

        try
        {
            Present(title, message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not report the failure to the user");
        }

        return true;
    }

    private void Present(string title, string message)
    {
        if (_present is not null)
        {
            _present(title, message);
            return;
        }

        try
        {
            if (_snackbar is not null)
            {
                _snackbar.Show(
                    title,
                    message,
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                    TimeSpan.FromSeconds(8)
                );
                return;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "The shell could not show the report; using a message box");
        }

        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error
        );
    }
}
