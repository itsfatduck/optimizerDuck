namespace optimizerDuck.Core.Models.Optimization;

public readonly record struct ApplyResult(string? Message)
{
    public static ApplyResult True()
    {
        return new ApplyResult(null);
    }

    public static ApplyResult False(string message)
    {
        return new ApplyResult(message);
    }
}