namespace DcamVision.Imaging;

public sealed class Tiff16CaptureWriter
{
    public async Task WriteAsync(CaptureRecord capture, Stream stream, CancellationToken cancellationToken = default)
    {
        capture.Frame.Validate();
        var width = capture.Frame.Width;
        var height = capture.Frame.Height;
        var pixelBytes = width * height * sizeof(ushort);
        const int ifdEntries = 10;
        const int headerBytes = 8;
        var ifdBytes = 2 + ifdEntries * 12 + 4;
        var pixelOffset = headerBytes + ifdBytes;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)8);
        writer.Write((ushort)ifdEntries);
        WriteEntry(writer, 256, 4, 1, (uint)width);
        WriteEntry(writer, 257, 4, 1, (uint)height);
        WriteEntry(writer, 258, 3, 1, 16);
        WriteEntry(writer, 259, 3, 1, 1);
        WriteEntry(writer, 262, 3, 1, 1);
        WriteEntry(writer, 273, 4, 1, (uint)pixelOffset);
        WriteEntry(writer, 277, 3, 1, 1);
        WriteEntry(writer, 278, 4, 1, (uint)height);
        WriteEntry(writer, 279, 4, 1, (uint)pixelBytes);
        WriteEntry(writer, 339, 3, 1, 1);
        writer.Write((uint)0);

        foreach (var pixel in capture.Frame.Pixels)
        {
            writer.Write(pixel);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(value);
    }
}
