namespace OctoPublisher.Tests;

public sealed class AnsiTerminalTests
{
    [Fact]
    public void StripAnsiRemovesAllColorAndStyleSequences()
    {
        var coloredText = "\u001b[38;2;96;205;255m[+] Header\u001b[0m: \u001b[38;5;244mValue\u001b[0m";
        var clean = AnsiTerminal.StripAnsi(coloredText);

        Assert.Equal("[+] Header: Value", clean);
    }

    [Fact]
    public void StripAnsiPreservesPlainText()
    {
        const string plain = "Plain ASCII text without escapes";
        Assert.Equal(plain, AnsiTerminal.StripAnsi(plain));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 100)]
    [InlineData(100, 100)]
    [InlineData(-10, 100)]
    [InlineData(150, 100)]
    public void ProgressBarDoesNotThrowOnVariousInputs(long current, long total)
    {
        var exception = Record.Exception(() =>
        {
            AnsiTerminal.ProgressBar(current, total, 1024 * 1024);
            AnsiTerminal.EndProgressBar();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void OutputMethodsDoNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            AnsiTerminal.Info("Test Info");
            AnsiTerminal.Success("Test Success");
            AnsiTerminal.Warning("Test Warning");
            AnsiTerminal.Error("Test Error");
            AnsiTerminal.Step("Step1", "Step Description");
            AnsiTerminal.Detail("Key", "Value");
            AnsiTerminal.Header("Test Header");
        });

        Assert.Null(exception);
    }
}
