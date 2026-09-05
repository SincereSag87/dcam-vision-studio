namespace DcamVision.Imaging;

public sealed class RawMono16CaptureWriter
{
    public async Task WriteAsync(CaptureRecord capture, Stream stream, CancellationToken cancellationToken = default)
    {
        capture.Frame.Validate();
        var buffer = new byte[capture.Frame.Pixels.Length * sizeof(ushort)];
        for (var i = 0; i < capture.Frame.Pixels.Length; i++)
        {
            var pixel = capture.Frame.Pixels[i];
            buffer[i * 2] = (byte)(pixel & 0xFF);
            buffer[i * 2 + 1] = (byte)(pixel >> 8);
        }

        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
