namespace optimizerDuck.Core.Models.Revert;

/// <summary>
///     Represents the result of validating revert data before executing a revert operation.
/// </summary>
public class RevertValidationResult
{
    private RevertValidationResult(bool valid, string message, string localizedMessage)
    {
        IsValid = valid;
        Message = message;
        LocalizedMessage = localizedMessage;
    }

    /// <summary>
    ///     Indicates whether the revert data passed validation.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    ///     A technical message describing the validation result (English).
    /// </summary>
    public string Message { get; private set; }

    /// <summary>
    ///     A localized message suitable for display to the user.
    /// </summary>
    public string LocalizedMessage { get; private set; }

    /// <summary>
    ///     Creates a successful validation result.
    /// </summary>
    /// <returns>A valid <see cref="RevertValidationResult" />.</returns>
    public static RevertValidationResult Success()
    {
        return new RevertValidationResult(true, "Revert data is valid.", string.Empty);
    }

    /// <summary>
    ///     Creates a failed validation result with a descriptive message.
    /// </summary>
    /// <param name="localizedMessage">The localized error message for the user.</param>
    /// <param name="message">The technical error message.</param>
    /// <returns>An invalid <see cref="RevertValidationResult" />.</returns>
    public static RevertValidationResult Fail(string localizedMessage, string message)
    {
        return new RevertValidationResult(false, message, localizedMessage);
    }
}