using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.Models.Revert;

public class RevertValidationResult
{
    private RevertValidationResult(bool valid, string message, string localizedMessage)
    {
        IsValid = valid;
        Message = message;
        LocalizedMessage = localizedMessage;
    }

    public bool IsValid { get; private set; }
    public string Message { get; private set; }
    public string LocalizedMessage { get; private set; }

    public static RevertValidationResult Success()
    {
        return new RevertValidationResult(true, "Revert data is valid.", Translations.Revert_Error_InvalidData);
    }

    public static RevertValidationResult Fail(string localizedMessage, string message)
    {
        return new RevertValidationResult(false, message, localizedMessage);
    }
}