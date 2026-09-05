namespace DcamVision.Core;

[Flags]
public enum CameraPropertyAccess
{
    None = 0,
    Read = 1,
    Write = 2,
    WriteWhileStreaming = 4
}
