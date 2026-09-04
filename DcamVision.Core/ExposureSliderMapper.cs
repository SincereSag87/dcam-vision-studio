namespace DcamVision.Core;

public sealed class ExposureSliderMapper
{
    private readonly double _minimumLog;
    private readonly double _maximumLog;

    public ExposureSliderMapper(CameraExposureRange range)
    {
        if (range.Minimum <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The exposure range minimum must be greater than zero.");
        }

        if (range.Maximum <= range.Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "The exposure range maximum must be greater than the minimum.");
        }

        Range = range;
        _minimumLog = Math.Log10(range.Minimum.TotalMicroseconds);
        _maximumLog = Math.Log10(range.Maximum.TotalMicroseconds);
    }

    public CameraExposureRange Range { get; }

    public double ToSliderValue(TimeSpan exposure)
    {
        var clamped = exposure < Range.Minimum ? Range.Minimum : exposure > Range.Maximum ? Range.Maximum : exposure;
        var value = (Math.Log10(clamped.TotalMicroseconds) - _minimumLog) / (_maximumLog - _minimumLog);
        return Math.Clamp(value * 100.0, 0.0, 100.0);
    }

    public TimeSpan FromSliderValue(double sliderValue)
    {
        var normalized = Math.Clamp(sliderValue, 0.0, 100.0) / 100.0;
        var microseconds = Math.Pow(10, _minimumLog + normalized * (_maximumLog - _minimumLog));
        return TimeSpan.FromMicroseconds(microseconds);
    }
}
