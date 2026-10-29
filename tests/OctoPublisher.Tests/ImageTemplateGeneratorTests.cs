namespace OctoPublisher.Tests;

public sealed class ImageTemplateGeneratorTests
{
    [Theory]
    [InlineData(512, 512)]
    [InlineData(1024, 500)]
    [InlineData(300, 300)]
    [InlineData(1080, 1920)]
    public void GeneratePngCreatesValidPngWithExpectedDimensions(int width, int height)
    {
        var bytes = ImageTemplateGenerator.GeneratePng(width, height);
        Assert.True(bytes.Length > 24);

        var dims = ImageTemplateGenerator.ReadPngDimensions(bytes);
        _ = Assert.NotNull(dims);
        Assert.Equal(width, dims.Value.Width);
        Assert.Equal(height, dims.Value.Height);
    }

    [Fact]
    public void ReadPngDimensionsReturnsNullOnInvalidData()
    {
        byte[] invalidBytes = [1, 2, 3, 4, 5];
        var dims = ImageTemplateGenerator.ReadPngDimensions(invalidBytes);
        Assert.Null(dims);
    }

    [Fact]
    public void ReadPngDimensionsReturnsNullOnNonExistentFile()
    {
        var dims = ImageTemplateGenerator.ReadPngDimensions("non_existent_file.png");
        Assert.Null(dims);
    }
}
