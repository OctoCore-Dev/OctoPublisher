namespace Octo.Common.Graphics;

public static class ImageTemplateGenerator
{
    private static readonly uint[] t_crcTable = InitializeCrcTable();

    private static uint[] InitializeCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var entry = i;
            for (var j = 0; j < 8; j++)
            {
                entry = (entry & 1) != 0 ? 0xEDB88320 ^ (entry >> 1) : entry >> 1;
            }
            table[i] = entry;
        }
        return table;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        var c = crc;
        foreach (var b in data)
        {
            c = t_crcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }
        return c;
    }

    public static byte[] GeneratePng(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using var ms = new MemoryStream();

        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ms.Write(pngSignature);

        var ihdrData = new byte[13];
        ihdrData[0] = (byte)(width >> 24);
        ihdrData[1] = (byte)(width >> 16);
        ihdrData[2] = (byte)(width >> 8);
        ihdrData[3] = (byte)width;
        ihdrData[4] = (byte)(height >> 24);
        ihdrData[5] = (byte)(height >> 16);
        ihdrData[6] = (byte)(height >> 8);
        ihdrData[7] = (byte)height;
        ihdrData[8] = 8;
        ihdrData[9] = 6;
        ihdrData[10] = 0;
        ihdrData[11] = 0;
        ihdrData[12] = 0;

        WriteChunk(ms, "IHDR"u8, ihdrData);

        var stride = 1 + (width * 4);
        var rawData = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            rawData[rowOffset] = 0;

            for (var x = 0; x < width; x++)
            {
                var pixelOffset = rowOffset + 1 + (x * 4);
                var isAccentBar = (y >= (height - Math.Max(8, height / 32))) && (y < (height - 2));
                var isBorder = (x == 0) || (y == 0) || (x == (width - 1)) || (y == (height - 1));

                if (isAccentBar)
                {
                    rawData[pixelOffset] = 0;
                    rawData[pixelOffset + 1] = 120;
                    rawData[pixelOffset + 2] = 212;
                    rawData[pixelOffset + 3] = 255;
                }
                else if (isBorder)
                {
                    rawData[pixelOffset] = 60;
                    rawData[pixelOffset + 1] = 60;
                    rawData[pixelOffset + 2] = 60;
                    rawData[pixelOffset + 3] = 255;
                }
                else
                {
                    var subtleGrid = ((x % 32) == 0) || ((y % 32) == 0);
                    var bg = (byte)(subtleGrid ? 38 : 32);
                    rawData[pixelOffset] = bg;
                    rawData[pixelOffset + 1] = bg;
                    rawData[pixelOffset + 2] = bg;
                    rawData[pixelOffset + 3] = 255;
                }
            }
        }

        using var compressedStream = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressedStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(rawData);
            zlib.Flush();
        }

        WriteChunk(ms, "IDAT"u8, compressedStream.ToArray());
        WriteChunk(ms, "IEND"u8, []);

        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        Span<byte> lenBytes = [(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length];
        stream.Write(lenBytes);
        stream.Write(type);
        if (length > 0)
        {
            stream.Write(data);
        }

        var crc = UpdateCrc(0xFFFFFFFF, type);
        if (length > 0)
        {
            crc = UpdateCrc(crc, data);
        }
        crc ^= 0xFFFFFFFF;

        Span<byte> crcBytes = [(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc];
        stream.Write(crcBytes);
    }

    public static (int Width, int Height)? ReadDimensions(byte[] bytes) => bytes == null ? null : ReadDimensions(bytes.AsSpan());

    public static (int Width, int Height)? ReadDimensions(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24)
        {
            return null;
        }

        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes.StartsWith(pngSignature))
        {
            ReadOnlySpan<byte> ihdrType = [0x49, 0x48, 0x44, 0x52];
            if (bytes.Length >= 24 && bytes.Slice(12, 4).SequenceEqual(ihdrType))
            {
                var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                return width > 0 && height > 0 ? (width, height) : null;
            }
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var i = 2;
            while (i < bytes.Length - 8)
            {
                if (bytes[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                var marker = bytes[i + 1];
                if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3)
                {
                    var h = (bytes[i + 5] << 8) | bytes[i + 6];
                    var w = (bytes[i + 7] << 8) | bytes[i + 8];
                    return w > 0 && h > 0 ? (w, h) : null;
                }

                if (marker is 0xD9 or 0xDA)
                {
                    break;
                }

                if (i + 3 >= bytes.Length)
                {
                    break;
                }

                var len = (bytes[i + 2] << 8) | bytes[i + 3];
                if (len < 2)
                {
                    break;
                }
                i += 2 + len;
            }
        }

        return null;
    }

    public static (int Width, int Height)? ReadPngDimensions(byte[] bytes) => ReadDimensions(bytes);

    public static (int Width, int Height)? ReadPngDimensions(string filePath) => ReadDimensions(filePath);

    public static (int Width, int Height)? ReadDimensions(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 24)
            {
                return null;
            }

            var buffer = new byte[Math.Min(fs.Length, 65536)];
            var read = fs.Read(buffer, 0, buffer.Length);
            return read < 24 ? null : ReadDimensions(buffer.AsSpan(0, read));
        }
        catch
        {
            return null;
        }
    }
}
