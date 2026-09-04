using DcamVision.Core;

namespace DcamVision.App.Services;

public static class CameraStatusFormatter
{
    public static string FormatHeaderStatus(CameraConnectionState state)
    {
        return state switch
        {
            CameraConnectionState.Disconnected => "Disconnected",
            CameraConnectionState.Discovering => "Discovering",
            CameraConnectionState.Connecting => "Connecting",
            CameraConnectionState.Connected => "Connected",
            CameraConnectionState.Streaming => "Live",
            _ => state.ToString()
        };
    }

    public static string FormatDeviceName(CameraDevice? device)
    {
        return device is null ? "No device connected" : device.DisplayName;
    }
}
