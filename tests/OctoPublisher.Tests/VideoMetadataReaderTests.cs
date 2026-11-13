namespace OctoPublisher.Tests;

public sealed class VideoMetadataReaderTests
{
    [Fact]
    public void ReadDimensionsReturnsNullWhenFileDoesNotExist()
    {
        var dims = VideoMetadataReader.ReadDimensions("non_existent_video_file.mp4");
        Assert.Null(dims);
    }

    [Fact]
    public void ReadDimensionsParsesMp4TkhdBoxCorrectly()
    {
        using var ms = new MemoryStream();

        // 1. создаем tkhd box (версия 0: размер 92 байта)
        var tkhdData = new byte[84];
        tkhdData[0] = 0; // version 0
        // width offset 76: 1920 (0x0780)
        tkhdData[76] = 0x07;
        tkhdData[77] = 0x80;
        tkhdData[78] = 0x00;
        tkhdData[79] = 0x00;
        // height offset 80: 1080 (0x0438)
        tkhdData[80] = 0x04;
        tkhdData[81] = 0x38;
        tkhdData[82] = 0x00;
        tkhdData[83] = 0x00;

        var tkhdBox = CreateBox("tkhd"u8, tkhdData);
        var trakBox = CreateBox("trak"u8, tkhdBox);
        var moovBox = CreateBox("moov"u8, trakBox);

        // ftyp box в начале
        var ftypBox = CreateBox("ftyp"u8, "mp42"u8.ToArray());

        ms.Write(ftypBox);
        ms.Write(moovBox);
        ms.Position = 0;

        var dims = VideoMetadataReader.ReadDimensions(ms);
        _ = Assert.NotNull(dims);
        Assert.Equal(1920, dims.Value.Width);
        Assert.Equal(1080, dims.Value.Height);
    }

    private static byte[] CreateBox(ReadOnlySpan<byte> boxType, byte[] payload)
    {
        var boxSize = 8 + payload.Length;
        var box = new byte[boxSize];
        box[0] = (byte)(boxSize >> 24);
        box[1] = (byte)(boxSize >> 16);
        box[2] = (byte)(boxSize >> 8);
        box[3] = (byte)boxSize;
        boxType.CopyTo(box.AsSpan(4, 4));
        payload.CopyTo(box.AsSpan(8));
        return box;
    }
}
