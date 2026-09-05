using System.Runtime.InteropServices;

namespace DcamVision.Dcam.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct DcamApiInit
{
    public int Size;
    public int DeviceCount;
    public int Reserved;
    public int InitOptionBytes;
    public IntPtr InitOption;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamDevOpen
{
    public int Size;
    public int Index;
    public IntPtr Hdcam;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamDevString
{
    public int Size;
    public int StringId;
    public IntPtr Text;
    public int TextBytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamPropertyAttribute
{
    public int Size;
    public int PropertyId;
    public int Option;
    public int Attribute;
    public int Group;
    public int Unit;
    public int Attribute2;
    public double Minimum;
    public double Maximum;
    public double Step;
    public double DefaultValue;
    public int TextBytes;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamPropertyValueText
{
    public int Size;
    public int PropertyId;
    public double Value;
    public IntPtr Text;
    public int TextBytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamBufferFrame
{
    public int Size;
    public int Index;
    public int Width;
    public int Height;
    public int RowBytes;
    public int Type;
    public IntPtr Buffer;
    public int Left;
    public int Top;
    public int TimestampSeconds;
    public int TimestampMicroseconds;
    public int FrameStamp;
    public int CameraStamp;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamWaitOpen
{
    public int Size;
    public IntPtr Hdcam;
    public IntPtr Hwait;
    public IntPtr SupportEvent;
}

[StructLayout(LayoutKind.Sequential)]
public struct DcamWaitStart
{
    public int Size;
    public int EventMask;
    public int TimeoutMilliseconds;
    public int EventHappened;
}
