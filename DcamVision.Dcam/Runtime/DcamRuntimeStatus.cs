namespace DcamVision.Dcam.Runtime;

public sealed record DcamRuntimeStatus(
    bool IsAvailable,
    string? UnavailableReason = null,
    int DeviceCount = 0,
    string? ApiVersion = null)
{
    public static DcamRuntimeStatus Available(int deviceCount, string? apiVersion = null) => new(true, null, deviceCount, apiVersion);

    public static DcamRuntimeStatus Unavailable(string reason) => new(false, reason);
}
