using DcamVision.Core;
using DcamVision.Dcam.Acquisition;
using DcamVision.Dcam.Devices;
using DcamVision.Dcam.Runtime;

namespace DcamVision.Dcam.Interop;

public interface IDcamNativeApi
{
    DcamRuntimeStatus CheckRuntime();

    int Initialize(out int deviceCount);

    int Uninitialize();

    IReadOnlyList<DcamDeviceInfo> EnumerateDevices(int deviceCount);

    IntPtr OpenDevice(int index);

    void CloseDevice(IntPtr deviceHandle);

    IReadOnlyList<CameraProperty> EnumerateProperties(IntPtr deviceHandle);

    CameraProperty? GetProperty(IntPtr deviceHandle, string propertyId);

    CameraProperty SetProperty(IntPtr deviceHandle, CameraProperty property, object value);

    TimeSpan GetExposure(IntPtr deviceHandle);

    CameraExposureRange GetExposureRange(IntPtr deviceHandle);

    CaptureSettings GetCaptureSettings(IntPtr deviceHandle);

    DcamRawFrame CaptureFrame(IntPtr deviceHandle, TimeSpan timeout, CancellationToken cancellationToken);

    IAsyncEnumerable<DcamRawFrame> StreamFramesAsync(
        IntPtr deviceHandle,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
