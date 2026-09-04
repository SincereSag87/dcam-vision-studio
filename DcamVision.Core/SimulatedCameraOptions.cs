namespace DcamVision.Core;

public sealed record SimulatedCameraOptions
{
    public double FramesPerSecond { get; init; } = 15.0;
}
