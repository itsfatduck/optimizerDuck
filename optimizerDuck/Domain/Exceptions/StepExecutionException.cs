namespace optimizerDuck.Domain.Exceptions;

public class StepExecutionException : Exception
{
    public string? ErrorDetail { get; }

    public StepExecutionException(string? error, string? errorDetail)
        : base(error ?? "Step execution failed")
    {
        ErrorDetail = errorDetail;
    }
}
