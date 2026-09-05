using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using DcamVision.Core;
using DcamVision.Dcam.Acquisition;
using DcamVision.Dcam.Devices;
using DcamVision.Dcam.Errors;
using DcamVision.Dcam.Properties;
using DcamVision.Dcam.Runtime;

namespace DcamVision.Dcam.Interop;

public sealed class DcamNativeApi : IDcamNativeApi, IDisposable
{
    private readonly ConcurrentDictionary<IntPtr, int> _deviceIndexes = new();
    private readonly object _syncRoot = new();
    private IntPtr _libraryHandle;
    private bool _disposed;

    private DcamApiInitDelegate? _dcamapiInit;
    private DcamApiUninitDelegate? _dcamapiUninit;
    private DcamDevOpenDelegate? _dcamdevOpen;
    private DcamDevCloseDelegate? _dcamdevClose;
    private DcamDevGetStringDelegate? _dcamdevGetString;
    private DcamPropGetNextIdDelegate? _dcampropGetNextId;
    private DcamPropGetNameDelegate? _dcampropGetName;
    private DcamPropGetAttrDelegate? _dcampropGetAttr;
    private DcamPropGetValueDelegate? _dcampropGetValue;
    private DcamPropSetValueDelegate? _dcampropSetValue;
    private DcamPropGetValueTextDelegate? _dcampropGetValueText;
    private DcamBufAllocDelegate? _dcambufAlloc;
    private DcamBufReleaseDelegate? _dcambufRelease;
    private DcamBufLockFrameDelegate? _dcambufLockFrame;
    private DcamCapStartDelegate? _dcamcapStart;
    private DcamCapStopDelegate? _dcamcapStop;
    private DcamWaitOpenDelegate? _dcamwaitOpen;
    private DcamWaitCloseDelegate? _dcamwaitClose;
    private DcamWaitStartDelegate? _dcamwaitStart;

    public DcamRuntimeStatus CheckRuntime()
    {
        try
        {
            EnsureLoaded();
            return DcamRuntimeStatus.Available(0);
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or InvalidOperationException)
        {
            return DcamRuntimeStatus.Unavailable(exception.Message);
        }
    }

    public int Initialize(out int deviceCount)
    {
        EnsureLoaded();
        var init = new DcamApiInit
        {
            Size = Marshal.SizeOf<DcamApiInit>()
        };

        var result = _dcamapiInit!(ref init);
        deviceCount = result >= DcamConstants.Success ? init.DeviceCount : 0;
        return result;
    }

    public int Uninitialize()
    {
        EnsureLoaded();
        _dcamapiUninit!();
        return DcamConstants.Success;
    }

    public IReadOnlyList<DcamDeviceInfo> EnumerateDevices(int deviceCount)
    {
        var devices = new List<DcamDeviceInfo>();
        for (var index = 0; index < deviceCount; index++)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenDevice(index);
                var vendor = ReadDeviceString(handle, DcamConstants.DeviceStringVendor) ?? "Hamamatsu";
                var model = ReadDeviceString(handle, DcamConstants.DeviceStringModel) ?? $"DCAM Camera {index}";
                var cameraId = ReadDeviceString(handle, DcamConstants.DeviceStringCameraId) ?? $"DCAM-{index}";
                devices.Add(new DcamDeviceInfo
                {
                    Index = index,
                    DeviceId = $"dcam:{index}:{SanitizeId(cameraId)}",
                    Vendor = vendor,
                    Model = model,
                    CameraId = cameraId,
                    Bus = ReadDeviceString(handle, DcamConstants.DeviceStringBus),
                    CameraVersion = ReadDeviceString(handle, DcamConstants.DeviceStringCameraVersion),
                    DriverVersion = ReadDeviceString(handle, DcamConstants.DeviceStringDriverVersion),
                    ModuleVersion = ReadDeviceString(handle, DcamConstants.DeviceStringModuleVersion),
                    DcamApiVersion = ReadDeviceString(handle, DcamConstants.DeviceStringDcamApiVersion)
                });
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    CloseDevice(handle);
                }
            }
        }

        return devices;
    }

    public IntPtr OpenDevice(int index)
    {
        EnsureLoaded();
        var open = new DcamDevOpen
        {
            Size = Marshal.SizeOf<DcamDevOpen>(),
            Index = index
        };

        DcamErrorTranslator.ThrowIfFailed(_dcamdevOpen!(ref open), "dcamdev_open", "Unable to open Hamamatsu camera.");
        _deviceIndexes[open.Hdcam] = index;
        return open.Hdcam;
    }

    public void CloseDevice(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return;
        }

        EnsureLoaded();
        _deviceIndexes.TryRemove(deviceHandle, out _);
        DcamErrorTranslator.ThrowIfFailed(_dcamdevClose!(deviceHandle), "dcamdev_close", "Unable to close Hamamatsu camera.");
    }

    public IReadOnlyList<CameraProperty> EnumerateProperties(IntPtr deviceHandle)
    {
        EnsureLoaded();
        var properties = new List<CameraProperty>();
        var propertyId = 0;

        while (true)
        {
            var result = _dcampropGetNextId!(deviceHandle, ref propertyId, DcamConstants.PropertyOptionNext);
            if (result < DcamConstants.Success)
            {
                break;
            }

            var property = ReadProperty(deviceHandle, propertyId);
            if (property is not null)
            {
                properties.Add(property);
            }
        }

        return properties.OrderBy(property => property.DisplayOrder).ThenBy(property => property.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public CameraProperty? GetProperty(IntPtr deviceHandle, string propertyId)
    {
        if (!DcamPropertyMapper.TryGetNativePropertyId(propertyId, out var nativeId))
        {
            return EnumerateProperties(deviceHandle).FirstOrDefault(property => string.Equals(property.Id, propertyId, StringComparison.OrdinalIgnoreCase));
        }

        return ReadProperty(deviceHandle, nativeId);
    }

    public CameraProperty SetProperty(IntPtr deviceHandle, CameraProperty property, object value)
    {
        if (!DcamPropertyMapper.TryGetNativePropertyId(property.Id, out var nativeId))
        {
            throw new InvalidOperationException($"Camera property '{property.Id}' cannot be mapped to a DCAM property id.");
        }

        var nativeValue = DcamPropertyMapper.ToNativeValue(property, value);
        DcamErrorTranslator.ThrowIfFailed(_dcampropSetValue!(deviceHandle, nativeId, nativeValue), "dcamprop_setvalue", $"Unable to update {property.DisplayName}.");
        return ReadProperty(deviceHandle, nativeId) ?? property with { Value = value };
    }

    public TimeSpan GetExposure(IntPtr deviceHandle)
    {
        var result = _dcampropGetValue!(deviceHandle, DcamConstants.PropertyExposureTime, out var seconds);
        DcamErrorTranslator.ThrowIfFailed(result, "dcamprop_getvalue", "Unable to read camera exposure.");
        return TimeSpan.FromSeconds(seconds);
    }

    public CameraExposureRange GetExposureRange(IntPtr deviceHandle)
    {
        var attribute = ReadAttribute(deviceHandle, DcamConstants.PropertyExposureTime);
        return new CameraExposureRange(TimeSpan.FromSeconds(attribute.Minimum), TimeSpan.FromSeconds(attribute.Maximum));
    }

    public CaptureSettings GetCaptureSettings(IntPtr deviceHandle)
    {
        var exposure = GetExposure(deviceHandle);
        var width = ReadIntProperty(deviceHandle, DcamConstants.PropertyImageWidth, 512);
        var height = ReadIntProperty(deviceHandle, DcamConstants.PropertyImageHeight, 384);
        var pixelType = ReadIntProperty(deviceHandle, DcamConstants.PropertyImagePixelType, DcamConstants.PixelTypeMono16);
        var triggerMode = ReadProperty(deviceHandle, DcamConstants.PropertyTriggerSource)?.FormatValue() ?? "Internal";

        return new CaptureSettings
        {
            Exposure = exposure,
            Width = width,
            Height = height,
            PixelFormat = DcamFrameConverter.MapPixelFormat(pixelType),
            TriggerMode = triggerMode
        };
    }

    public DcamRawFrame CaptureFrame(IntPtr deviceHandle, TimeSpan timeout, CancellationToken cancellationToken)
    {
        EnsureLoaded();
        DcamErrorTranslator.ThrowIfFailed(_dcambufAlloc!(deviceHandle, 4), "dcambuf_alloc", "Unable to allocate DCAM frame buffers.");
        try
        {
            DcamErrorTranslator.ThrowIfFailed(_dcamcapStart!(deviceHandle, DcamConstants.CaptureSnap), "dcamcap_start", "Unable to start camera capture.");
            WaitForFrame(deviceHandle, timeout, cancellationToken);
            return LockAndCopyFrame(deviceHandle, -1);
        }
        finally
        {
            TryStopAndRelease(deviceHandle);
        }
    }

    public async IAsyncEnumerable<DcamRawFrame> StreamFramesAsync(
        IntPtr deviceHandle,
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureLoaded();
        DcamErrorTranslator.ThrowIfFailed(_dcambufAlloc!(deviceHandle, 8), "dcambuf_alloc", "Unable to allocate DCAM streaming buffers.");
        try
        {
            DcamErrorTranslator.ThrowIfFailed(_dcamcapStart!(deviceHandle, DcamConstants.CaptureSequence), "dcamcap_start", "Unable to start camera streaming.");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => WaitForFrame(deviceHandle, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                yield return LockAndCopyFrame(deviceHandle, -1);
            }
        }
        finally
        {
            TryStopAndRelease(deviceHandle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }
    }

    private void EnsureLoaded()
    {
        lock (_syncRoot)
        {
            if (_libraryHandle != IntPtr.Zero)
            {
                return;
            }

            if (!NativeLibrary.TryLoad(DcamConstants.NativeLibraryName, out _libraryHandle))
            {
                throw new DllNotFoundException("Hamamatsu DCAM runtime was not found. Install the supported DCAM-API runtime and camera driver, then restart DCAM Vision Studio.");
            }

            try
            {
                _dcamapiInit = GetExport<DcamApiInitDelegate>("dcamapi_init");
                _dcamapiUninit = GetExport<DcamApiUninitDelegate>("dcamapi_uninit");
                _dcamdevOpen = GetExport<DcamDevOpenDelegate>("dcamdev_open");
                _dcamdevClose = GetExport<DcamDevCloseDelegate>("dcamdev_close");
                _dcamdevGetString = GetExport<DcamDevGetStringDelegate>("dcamdev_getstring");
                _dcampropGetNextId = GetExport<DcamPropGetNextIdDelegate>("dcamprop_getnextid");
                _dcampropGetName = GetExport<DcamPropGetNameDelegate>("dcamprop_getname");
                _dcampropGetAttr = GetExport<DcamPropGetAttrDelegate>("dcamprop_getattr");
                _dcampropGetValue = GetExport<DcamPropGetValueDelegate>("dcamprop_getvalue");
                _dcampropSetValue = GetExport<DcamPropSetValueDelegate>("dcamprop_setvalue");
                _dcampropGetValueText = GetExport<DcamPropGetValueTextDelegate>("dcamprop_getvaluetext");
                _dcambufAlloc = GetExport<DcamBufAllocDelegate>("dcambuf_alloc");
                _dcambufRelease = GetExport<DcamBufReleaseDelegate>("dcambuf_release");
                _dcambufLockFrame = GetExport<DcamBufLockFrameDelegate>("dcambuf_lockframe");
                _dcamcapStart = GetExport<DcamCapStartDelegate>("dcamcap_start");
                _dcamcapStop = GetExport<DcamCapStopDelegate>("dcamcap_stop");
                _dcamwaitOpen = GetExport<DcamWaitOpenDelegate>("dcamwait_open");
                _dcamwaitClose = GetExport<DcamWaitCloseDelegate>("dcamwait_close");
                _dcamwaitStart = GetExport<DcamWaitStartDelegate>("dcamwait_start");
            }
            catch
            {
                NativeLibrary.Free(_libraryHandle);
                _libraryHandle = IntPtr.Zero;
                throw;
            }
        }
    }

    private T GetExport<T>(string name)
        where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_libraryHandle, name, out var address))
        {
            throw new EntryPointNotFoundException($"The installed DCAM runtime does not expose '{name}'.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private string? ReadDeviceString(IntPtr deviceHandle, int stringId)
    {
        var buffer = new byte[256];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var request = new DcamDevString
            {
                Size = Marshal.SizeOf<DcamDevString>(),
                StringId = stringId,
                Text = handle.AddrOfPinnedObject(),
                TextBytes = buffer.Length
            };

            var result = _dcamdevGetString!(deviceHandle, ref request);
            if (result < DcamConstants.Success)
            {
                return null;
            }

            return Encoding.ASCII.GetString(buffer).TrimEnd('\0').Trim();
        }
        finally
        {
            handle.Free();
        }
    }

    private CameraProperty? ReadProperty(IntPtr deviceHandle, int nativePropertyId)
    {
        if (_dcampropGetValue!(deviceHandle, nativePropertyId, out var value) < DcamConstants.Success)
        {
            return null;
        }

        var attribute = ReadAttribute(deviceHandle, nativePropertyId);
        var name = ReadPropertyName(deviceHandle, nativePropertyId) ?? $"DCAM 0x{nativePropertyId:X8}";
        var options = ReadOptions(deviceHandle, nativePropertyId, attribute);
        return DcamPropertyMapper.MapProperty(nativePropertyId, name, value, attribute, options);
    }

    private DcamPropertyAttribute ReadAttribute(IntPtr deviceHandle, int nativePropertyId)
    {
        var attribute = new DcamPropertyAttribute
        {
            Size = Marshal.SizeOf<DcamPropertyAttribute>(),
            PropertyId = nativePropertyId
        };
        DcamErrorTranslator.ThrowIfFailed(_dcampropGetAttr!(deviceHandle, ref attribute), "dcamprop_getattr", "Unable to read DCAM property metadata.");
        return attribute;
    }

    private string? ReadPropertyName(IntPtr deviceHandle, int nativePropertyId)
    {
        var buffer = new byte[128];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var result = _dcampropGetName!(deviceHandle, nativePropertyId, handle.AddrOfPinnedObject(), buffer.Length);
            return result < DcamConstants.Success ? null : Encoding.ASCII.GetString(buffer).TrimEnd('\0').Trim();
        }
        finally
        {
            handle.Free();
        }
    }

    private IReadOnlyList<CameraPropertyOption> ReadOptions(IntPtr deviceHandle, int nativePropertyId, DcamPropertyAttribute attribute)
    {
        if (attribute.Group != DcamConstants.PropertyTypeMode)
        {
            return [];
        }

        var options = new List<CameraPropertyOption>();
        var start = Math.Ceiling(attribute.Minimum);
        var end = Math.Floor(attribute.Maximum);
        for (var optionValue = start; optionValue <= end && options.Count < 128; optionValue++)
        {
            var text = ReadValueText(deviceHandle, nativePropertyId, optionValue);
            if (!string.IsNullOrWhiteSpace(text))
            {
                options.Add(new CameraPropertyOption(optionValue, text));
            }
        }

        return options;
    }

    private string? ReadValueText(IntPtr deviceHandle, int nativePropertyId, double value)
    {
        var buffer = new byte[128];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var request = new DcamPropertyValueText
            {
                Size = Marshal.SizeOf<DcamPropertyValueText>(),
                PropertyId = nativePropertyId,
                Value = value,
                Text = handle.AddrOfPinnedObject(),
                TextBytes = buffer.Length
            };

            var result = _dcampropGetValueText!(deviceHandle, ref request);
            return result < DcamConstants.Success ? null : Encoding.ASCII.GetString(buffer).TrimEnd('\0').Trim();
        }
        finally
        {
            handle.Free();
        }
    }

    private int ReadIntProperty(IntPtr deviceHandle, int propertyId, int fallback)
    {
        return _dcampropGetValue!(deviceHandle, propertyId, out var value) < DcamConstants.Success ? fallback : (int)Math.Round(value);
    }

    private void WaitForFrame(IntPtr deviceHandle, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var waitOpen = new DcamWaitOpen
        {
            Size = Marshal.SizeOf<DcamWaitOpen>(),
            Hdcam = deviceHandle
        };

        DcamErrorTranslator.ThrowIfFailed(_dcamwaitOpen!(ref waitOpen), "dcamwait_open", "Unable to create DCAM wait handle.");
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wait = new DcamWaitStart
                {
                    Size = Marshal.SizeOf<DcamWaitStart>(),
                    EventMask = DcamConstants.WaitEventFrameready | DcamConstants.WaitEventStopped,
                    TimeoutMilliseconds = (int)Math.Clamp(Math.Min(timeout.TotalMilliseconds, 100), 1, int.MaxValue)
                };

                var result = _dcamwaitStart!(waitOpen.Hwait, ref wait);
                if (result >= DcamConstants.Success && (wait.EventHappened & DcamConstants.WaitEventFrameready) != 0)
                {
                    return;
                }

                if (result < DcamConstants.Success && result != DcamConstants.ErrorTimeout)
                {
                    DcamErrorTranslator.ThrowIfFailed(result, "dcamwait_start", "Unable to wait for a DCAM frame.");
                }
            }
        }
        finally
        {
            _dcamwaitClose!(waitOpen.Hwait);
        }
    }

    private DcamRawFrame LockAndCopyFrame(IntPtr deviceHandle, int index)
    {
        var frame = new DcamBufferFrame
        {
            Size = Marshal.SizeOf<DcamBufferFrame>(),
            Index = index
        };
        DcamErrorTranslator.ThrowIfFailed(_dcambufLockFrame!(deviceHandle, ref frame), "dcambuf_lockframe", "Unable to lock DCAM frame.");

        var bytesPerPixel = frame.Type == DcamConstants.PixelTypeMono8 ? 1 : 2;
        var bytes = new byte[checked(frame.RowBytes * frame.Height)];
        Marshal.Copy(frame.Buffer, bytes, 0, bytes.Length);
        DateTimeOffset? timestamp = null;
        if (frame.TimestampSeconds > 0)
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(frame.TimestampSeconds).AddTicks(frame.TimestampMicroseconds * 10L);
        }

        if (frame.Width <= 0 || frame.Height <= 0 || frame.RowBytes < frame.Width * bytesPerPixel)
        {
            throw new InvalidOperationException("DCAM returned an invalid frame layout.");
        }

        return new DcamRawFrame
        {
            Width = frame.Width,
            Height = frame.Height,
            RowBytes = frame.RowBytes,
            PixelType = frame.Type,
            Buffer = bytes,
            FrameStamp = frame.FrameStamp,
            Timestamp = timestamp
        };
    }

    private void TryStopAndRelease(IntPtr deviceHandle)
    {
        try
        {
            _dcamcapStop?.Invoke(deviceHandle);
        }
        finally
        {
            _dcambufRelease?.Invoke(deviceHandle, 0);
        }
    }

    private static string SanitizeId(string value)
    {
        return string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamApiInitDelegate(ref DcamApiInit init);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DcamApiUninitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamDevOpenDelegate(ref DcamDevOpen open);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamDevCloseDelegate(IntPtr hdcam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamDevGetStringDelegate(IntPtr hdcam, ref DcamDevString text);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropGetNextIdDelegate(IntPtr hdcam, ref int propertyId, int option);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropGetNameDelegate(IntPtr hdcam, int propertyId, IntPtr text, int textBytes);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropGetAttrDelegate(IntPtr hdcam, ref DcamPropertyAttribute attribute);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropGetValueDelegate(IntPtr hdcam, int propertyId, out double value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropSetValueDelegate(IntPtr hdcam, int propertyId, double value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamPropGetValueTextDelegate(IntPtr hdcam, ref DcamPropertyValueText valueText);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamBufAllocDelegate(IntPtr hdcam, int frameCount);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamBufReleaseDelegate(IntPtr hdcam, int reserved);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamBufLockFrameDelegate(IntPtr hdcam, ref DcamBufferFrame frame);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamCapStartDelegate(IntPtr hdcam, int mode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamCapStopDelegate(IntPtr hdcam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamWaitOpenDelegate(ref DcamWaitOpen waitOpen);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamWaitCloseDelegate(IntPtr hwait);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DcamWaitStartDelegate(IntPtr hwait, ref DcamWaitStart waitStart);
}
