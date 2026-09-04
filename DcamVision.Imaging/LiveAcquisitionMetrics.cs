namespace DcamVision.Imaging;

public sealed record LiveAcquisitionMetrics(
    long FramesAcquired,
    long FramesProcessed,
    long FramesDisplayed,
    long PipelineDrops,
    long SourceFrameGaps,
    double AcquisitionFps,
    double DisplayFps,
    TimeSpan Elapsed,
    long LatestFrameNumber,
    int BufferOccupancy,
    int BufferCapacity)
{
    public long TotalDropped => PipelineDrops + SourceFrameGaps;

    public static LiveAcquisitionMetrics Empty(int bufferCapacity) => new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        TimeSpan.Zero,
        0,
        0,
        bufferCapacity);
}
