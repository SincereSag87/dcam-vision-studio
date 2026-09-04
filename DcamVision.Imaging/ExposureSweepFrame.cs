using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed record ExposureSweepFrame(
    TimeSpan Exposure,
    CameraFrame Frame,
    FrameStatistics Statistics);
