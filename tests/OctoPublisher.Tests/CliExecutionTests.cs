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

    [Theory]
    [InlineData("playstore")]
    [InlineData("playstore", "--help")]
    [InlineData("playstore", "-h")]
    public async Task MainReturnsZeroWhenPlayStoreHelpRequestedAsync(params string[] args)
    {
        var exitCode = await Program.Main(args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task MainReturnsOneWhenUnknownPlayStoreSubcommandAsync()
    {
        var exitCode = await Program.Main(["playstore", "unsupported-action"]);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task MainReturnsTwoWhenPlayStorePublishMissingRequiredFlagsAsync()
    {
        var exitCode = await Program.Main(["playstore", "publish"]);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task MainExecutesInitCommandSuccessfullyAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var exitCode = await Program.Main(["init", tempDir]);
            Assert.Equal(0, exitCode);

            Assert.False(File.Exists(Path.Combine(tempDir, "README.md")));
            Assert.True(File.Exists(Path.Combine(tempDir, "whatsnew", "whatsnew-ru-RU.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "ru-RU", "title.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "ru-RU", "short_description.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "ru-RU", "full_description.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "windows", "ru-RU", "features.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "ru-RU", "icon.png")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task MainExecutesAddCommandSuccessfullyAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            _ = await Program.Main(["init", tempDir]);
            var addCode = await Program.Main(["add", "android", "fr", "--dir", tempDir]);
            Assert.Equal(0, addCode);

            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "fr-FR", "title.txt")));
            Assert.True(File.Exists(Path.Combine(tempDir, "store", "android", "fr-FR", "icon.png")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task MainExecutesListCommandSuccessfullyAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            _ = await Program.Main(["init", tempDir]);
            var listCode = await Program.Main(["list", "--dir", tempDir]);
            Assert.Equal(0, listCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task MainExecutesTestCommandAndReturnsOneWhenPlaceholdersDetectedAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            _ = await Program.Main(["init", tempDir]);
            var testCode = await Program.Main(["test", "--dir", tempDir]);
            Assert.Equal(1, testCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task MainExecutesPullHelpAsync()
    {
        var exitCode = await Program.Main(["pull", "--help"]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task MainExecutesPullFailsWhenMissingArgumentsAsync()
    {
        var exitCode = await Program.Main(["pull", "windows"]);
        Assert.Equal(1, exitCode);
    }
}
