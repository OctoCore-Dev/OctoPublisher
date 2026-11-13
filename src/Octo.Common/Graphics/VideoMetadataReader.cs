namespace Octo.Common.Graphics;

public static class VideoMetadataReader
{
    private static readonly byte[] s_moov = "moov"u8.ToArray();
    private static readonly byte[] s_trak = "trak"u8.ToArray();
    private static readonly byte[] s_tkhd = "tkhd"u8.ToArray();

    public static (int Width, int Height)? ReadDimensions(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ReadDimensions(stream);
        }
        catch
        {
            return null;
        }
    }

    public static (int Width, int Height)? ReadDimensions(Stream stream)
    {
        //читаем атомы первого уровня в поисках moov
        while (stream.Position + 8 <= stream.Length)
        {
            var (boxSize, boxType, headerSize) = ReadBoxHeader(stream);
            if (boxSize < headerSize)
            {
                break;
            }

            if (boxType.SequenceEqual(s_moov))
            {
                return ReadMoov(stream, stream.Position + boxSize - headerSize);
            }

            //переходим к следующему атому
            var payloadSize = boxSize - headerSize;
            if (stream.Position + payloadSize > stream.Length)
            {
                break;
            }
            _ = stream.Seek(payloadSize, SeekOrigin.Current);
        }

        return null;
    }

    private static (int Width, int Height)? ReadMoov(Stream stream, long moovEnd)
    {
        while (stream.Position + 8 <= moovEnd)
        {
            var (boxSize, boxType, headerSize) = ReadBoxHeader(stream);
            if (boxSize < headerSize)
            {
                break;
            }

            if (boxType.SequenceEqual(s_trak))
            {
                var dims = ReadTrak(stream, stream.Position + boxSize - headerSize);
                if (dims.HasValue && dims.Value.Width > 0 && dims.Value.Height > 0)
                {
                    return dims;
                }
            }
            else
            {
                var payloadSize = boxSize - headerSize;
                if (stream.Position + payloadSize > moovEnd)
                {
                    break;
                }
                _ = stream.Seek(payloadSize, SeekOrigin.Current);
            }
        }

        return null;
    }

    private static (int Width, int Height)? ReadTrak(Stream stream, long trakEnd)
    {
        while (stream.Position + 8 <= trakEnd)
        {
            var (boxSize, boxType, headerSize) = ReadBoxHeader(stream);
            if (boxSize < headerSize)
            {
                break;
            }

            var payloadSize = boxSize - headerSize;

            if (boxType.SequenceEqual(s_tkhd))
            {
                var buffer = new byte[Math.Min(payloadSize, 120)];
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read >= 84)
                {
                    var version = buffer[0];
                    var widthOffset = version == 0 ? 76 : 88;
                    var heightOffset = widthOffset + 4;

                    if (read >= heightOffset + 4)
                    {
                        var width = (buffer[widthOffset] << 8) | buffer[widthOffset + 1];
                        var height = (buffer[heightOffset] << 8) | buffer[heightOffset + 1];

                        if (width > 0 && height > 0)
                        {
                            return (width, height);
                        }
                    }
                }

                //дочитываем остаток атома если есть
                if (payloadSize > read)
                {
                    _ = stream.Seek(payloadSize - read, SeekOrigin.Current);
                }
            }
            else
            {
                if (stream.Position + payloadSize > trakEnd)
                {
                    break;
                }
                _ = stream.Seek(payloadSize, SeekOrigin.Current);
            }
        }

        return null;
    }

    private static (long BoxSize, byte[] BoxType, int HeaderSize) ReadBoxHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        if (read < 8)
        {
            return (0, [], 0);
        }

        long boxSize = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
        var boxType = header.Slice(4, 4).ToArray();
        var headerSize = 8;

        if (boxSize == 1)
        {
            //расширенный 64-битный размер
            Span<byte> ext = stackalloc byte[8];
            var extRead = stream.Read(ext);
            if (extRead < 8)
            {
                return (0, [], 0);
            }
            boxSize = (long)(((ulong)ext[0] << 56) | ((ulong)ext[1] << 48) | ((ulong)ext[2] << 40) | ((ulong)ext[3] << 32)
                          | ((ulong)ext[4] << 24) | ((ulong)ext[5] << 16) | ((ulong)ext[6] << 8) | ext[7]);
            headerSize = 16;
        }
        else if (boxSize == 0)
        {
            boxSize = stream.Length - (stream.Position - 8);
        }

        return (boxSize, boxType, headerSize);
    }
}
