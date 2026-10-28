namespace OctoPublisher.Tests;

public sealed class PublisherResultTests
{
    [Fact]
    public void SuccessSetsPropertiesAndZeroExitCode()
    {
        var result = PublisherResult.Success("Operation succeeded", "SUB-123");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Operation succeeded", result.Message);
        Assert.Equal("SUB-123", result.OperationId);
    }

    [Fact]
    public void FailedSetsFailureAndSpecifiedExitCode()
    {
        var result = PublisherResult.Failed(2, "Auth error", "SUB-FAIL");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("Auth error", result.Message);
        Assert.Equal("SUB-FAIL", result.OperationId);
    }

    [Fact]
    public void FailedDefaultsToExitCodeOneWhenZeroPassed()
    {
        var result = PublisherResult.Failed(0, "Error");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }
}
