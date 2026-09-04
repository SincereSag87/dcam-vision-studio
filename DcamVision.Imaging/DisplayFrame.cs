namespace DcamVision.Imaging;

public sealed record DisplayFrame(
    byte[] Pixels,
    ushort EffectiveBlackPoint,
    ushort EffectiveWhitePoint,
    DisplayClippingStatistics Clipping,
    TimeSpan ProcessingTime);
