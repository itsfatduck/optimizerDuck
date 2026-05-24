using optimizerDuck.Domain.Exceptions;

namespace optimizerDuck.Test.Domain.Exceptions;

public class StepExecutionExceptionTests
{
    [Fact]
    public void Constructor_WithErrorAndDetail_SetsProperties()
    {
        var ex = new StepExecutionException("Something went wrong", "Stack trace details");

        Assert.Equal("Something went wrong", ex.Message);
        Assert.Equal("Stack trace details", ex.ErrorDetail);
    }

    [Fact]
    public void Constructor_WithNullError_UsesDefaultMessage()
    {
        var ex = new StepExecutionException(null, null);

        Assert.Equal("Step execution failed", ex.Message);
        Assert.Null(ex.ErrorDetail);
    }

    [Fact]
    public void Constructor_WithErrorOnly_SetsMessageAndNullDetail()
    {
        var ex = new StepExecutionException("Access denied", null);

        Assert.Equal("Access denied", ex.Message);
        Assert.Null(ex.ErrorDetail);
    }

    [Fact]
    public void Constructor_WithDetailOnly_DefaultMessageUsed()
    {
        var ex = new StepExecutionException(null, "Inner exception details");

        Assert.Equal("Step execution failed", ex.Message);
        Assert.Equal("Inner exception details", ex.ErrorDetail);
    }
}
