using System.IO.Compression;
using System.Text;

namespace DcamVision.Imaging;

public sealed class PngPreviewCaptureWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private readonly ImageDisplayProcessor _displayProcessor;

    public PngPreviewCaptureWriter(ImageDisplayProcessor displayProcessor)
    {
        _displayProcessor = displayProcessor;
    }

    public async Task WriteAsync(
        CaptureRecord capture,
        ImageDisplaySettings settings,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var display = _displayProcessor.Process(capture.Frame, settings);
        await stream.WriteAsync(Signature, cancellationToken).ConfigureAwait(false);
        await WriteChunkAsync(stream, "IHDR", CreateHeader(capture.Frame.Width, capture.Frame.Height), cancellationToken).ConfigureAwait(false);
        await WriteChunkAsync(stream, "IDAT", CompressRows(display.Pixels, capture.Frame.Width, capture.Frame.Height), cancellationToken).ConfigureAwait(false);
        await WriteChunkAsync(stream, "IEND", [], cancellationToken).ConfigureAwait(false);
    }

    private static byte[] CreateHeader(int width, int height)
    {
        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 0;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static byte[] CompressRows(byte[] pixels, int width, int height)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(pixels, y * width, width);
            }
        }

        return output.ToArray();
    }

    private static async Task WriteChunkAsync(Stream stream, string type, byte[] data, CancellationToken cancellationToken)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(typeBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        var crc = Crc32.Compute(crcInput);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, unchecked((int)crc));
        await stream.WriteAsync(crcBytes, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        public static uint Compute(byte[] bytes)
        {
            var crc = 0xFFFF_FFFFu;
            foreach (var value in bytes)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFF_FFFFu;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) == 1 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
