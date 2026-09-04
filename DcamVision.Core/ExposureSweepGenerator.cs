namespace DcamVision.Core;

public static class ExposureSweepGenerator
{
    public const int MaximumSweepPoints = 500;

    public static IReadOnlyList<TimeSpan> Generate(ExposureSweepSettings settings)
    {
        Validate(settings);

        var values = new List<TimeSpan>();
        var currentTicks = settings.Start.Ticks;
        var endTicks = settings.End.Ticks;
        var stepTicks = settings.Step.Ticks;

        while (currentTicks <= endTicks)
        {
            if (values.Count >= MaximumSweepPoints)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), $"Exposure sweep cannot exceed {MaximumSweepPoints} exposure points.");
            }

            values.Add(TimeSpan.FromTicks(currentTicks));
            currentTicks += stepTicks;
        }

        return values;
    }

    public static void Validate(ExposureSweepSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Start <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Sweep start exposure must be greater than zero.");
        }

        if (settings.End <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Sweep end exposure must be greater than zero.");
        }

        if (settings.Start > settings.End)
        {
            throw new ArgumentException("Sweep start exposure must be less than or equal to the end exposure.", nameof(settings));
        }

        if (settings.Step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Sweep step must be greater than zero.");
        }

        if (settings.FramesPerExposure < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Frames per exposure must be at least 1.");
        }

        if (settings.DelayBetweenCaptures < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Delay between captures cannot be negative.");
        }

        if (!settings.ExposureRange.Contains(settings.Start) || !settings.ExposureRange.Contains(settings.End))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Sweep start and end exposures must be within the supported camera range.");
        }
    }
}
