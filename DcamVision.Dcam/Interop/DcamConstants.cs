namespace DcamVision.Dcam.Interop;

public static class DcamConstants
{
    public const string NativeLibraryName = "dcamapi.dll";

    public const int Success = 1;
    public const int ErrorTimeout = unchecked((int)0x80000106);

    public const int DeviceStringVendor = 0x04000101;
    public const int DeviceStringCameraId = 0x04000102;
    public const int DeviceStringBus = 0x04000103;
    public const int DeviceStringModel = 0x04000104;
    public const int DeviceStringCameraVersion = 0x04000105;
    public const int DeviceStringDriverVersion = 0x04000106;
    public const int DeviceStringModuleVersion = 0x04000107;
    public const int DeviceStringDcamApiVersion = 0x04000108;

    public const int PropertyOptionNext = 0x01000000;
    public const int PropertyOptionNearest = unchecked((int)0x80000000);

    public const int PropertyAttributeReadable = 0x00010000;
    public const int PropertyAttributeWritable = 0x00020000;
    public const int PropertyAttributeHasRange = unchecked((int)0x80000000);
    public const int PropertyAttributeHasStep = 0x40000000;
    public const int PropertyAttributeHasDefault = 0x20000000;

    public const int PropertyTypeMode = 0x00000001;
    public const int PropertyTypeLong = 0x00000002;
    public const int PropertyTypeReal = 0x00000003;

    public const int PropertyExposureTime = 0x001F0110;
    public const int PropertyImageWidth = 0x00420210;
    public const int PropertyImageHeight = 0x00420220;
    public const int PropertyImagePixelType = 0x00420100;
    public const int PropertyTriggerSource = 0x00100110;

    public const int PixelTypeMono8 = 1;
    public const int PixelTypeMono16 = 2;

    public const int CaptureSequence = -1;
    public const int CaptureSnap = 0;

    public const int WaitEventFrameready = 0x0002;
    public const int WaitEventStopped = 0x0010;
}
