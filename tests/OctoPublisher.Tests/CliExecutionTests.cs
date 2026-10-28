namespace OctoPublisher.Tests;

public sealed class CliExecutionTests
{
    [Fact]
    public async Task MainReturnsZeroWhenEmptyArgsAsync()
    {
        var exitCode = await Program.Main([]);
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("help")]
    public async Task MainReturnsZeroWhenHelpRequestedAsync(string flag)
    {
        var exitCode = await Program.Main([flag]);
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    [InlineData("version")]
    public async Task MainReturnsZeroWhenVersionRequestedAsync(string flag)
    {
        var exitCode = await Program.Main([flag]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task MainReturnsOneWhenUnknownTopLevelCommandAsync()
    {
        var exitCode = await Program.Main(["unknown-command"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MainReturnsOneWhenUnknownMStoreSubcommandAsync()
    {
        var exitCode = await Program.Main(["mstore", "unsupported-action"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MainReturnsOneWhenPublishMissingPackageFlagAsync()
    {
        var exitCode = await Program.Main([
            "mstore", "publish",
            "--tenant-id", "test-tenant",
            "--client-id", "test-client",
            "--client-secret", "test-secret",
            "--app-id", "test-app"
        ]);

        Assert.Equal(1, exitCode);
    }
}
