using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed class ExposureSweepRunner
{
    private readonly ICameraService _cameraService;

    public ExposureSweepRunner(ICameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task<ExposureSweepResult> RunAsync(
        ExposureSweepSettings settings,
        IProgress<ExposureSweepProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_cameraService.ConnectedDevice is null)
        {
            throw new InvalidOperationException("Connect to a camera before running an exposure sweep.");
        }

        var exposures = ExposureSweepGenerator.Generate(settings with { ExposureRange = _cameraService.ExposureRange });
        var previousExposure = _cameraService.CurrentSettings.Exposure;
        var startedAt = DateTimeOffset.UtcNow;
        var frames = new List<ExposureSweepFrame>(exposures.Count * settings.FramesPerExposure);
        var totalFrames = exposures.Count * settings.FramesPerExposure;

        try
        {
            for (var exposureIndex = 0; exposureIndex < exposures.Count; exposureIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exposure = exposures[exposureIndex];
                await _cameraService.SetExposureAsync(exposure, cancellationToken).ConfigureAwait(false);

                for (var frameIndex = 0; frameIndex < settings.FramesPerExposure; frameIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frame = await _cameraService.CaptureAsync(cancellationToken).ConfigureAwait(false);
                    frames.Add(new ExposureSweepFrame(
                        exposure,
                        frame,
                        FrameStatisticsCalculator.Calculate(frame)));

                    progress?.Report(new ExposureSweepProgress(
                        exposureIndex + 1,
                        exposures.Count,
                        exposure,
                        frames.Count,
                        totalFrames));

                    if (settings.DelayBetweenCaptures > TimeSpan.Zero)
                    {
                        await Task.Delay(settings.DelayBetweenCaptures, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return new ExposureSweepResult(startedAt, DateTimeOffset.UtcNow, frames);
        }
        finally
        {
            await _cameraService.SetExposureAsync(previousExposure, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
