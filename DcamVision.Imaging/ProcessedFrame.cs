using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed record ProcessedFrame(
    CameraFrame Frame,
    FrameStatistics Statistics,
    int[] Histogram);
