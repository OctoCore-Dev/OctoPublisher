namespace OctoPublisher.Tests;

public sealed class PublisherResultTests
{
    [Fact]
    public void SuccessSetsPropertiesAndZeroExitCode()
    {
        var result = PublisherResult.Success("Operation succeeded", "SUB-123");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.IsQueued);
        Assert.Equal("Operation succeeded", result.Message);
        Assert.Equal("SUB-123", result.OperationId);
    }

    [Fact]
    public void FailedSetsFailureAndSpecifiedExitCode()
    {
        var result = PublisherResult.Failed(2, "Auth error", "SUB-FAIL");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.ExitCode);
        Assert.False(result.IsQueued);
        Assert.Equal("Auth error", result.Message);
        Assert.Equal("SUB-FAIL", result.OperationId);
    }

    [Fact]
    public void FailedDefaultsToExitCodeOneWhenZeroPassed()
    {
        var result = PublisherResult.Failed(0, "Error");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.False(result.IsQueued);
    }

    [Fact]
    public void QueuedSetsIsQueuedAndExitCodeThree()
    {
        var result = PublisherResult.Queued("Submission in review", "SUB-QUEUED");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsQueued);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Submission in review", result.Message);
        Assert.Equal("SUB-QUEUED", result.OperationId);
    }
}
