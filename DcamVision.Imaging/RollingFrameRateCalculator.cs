namespace DcamVision.Imaging;

public sealed class RollingFrameRateCalculator
{
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _samples = new();

    public RollingFrameRateCalculator(TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Frame-rate window must be greater than zero.");
        }

        _window = window;
    }

    public double Record(DateTimeOffset timestamp)
    {
        _samples.Enqueue(timestamp);
        Trim(timestamp);
        return FramesPerSecond(timestamp);
    }

    public double FramesPerSecond(DateTimeOffset now)
    {
        Trim(now);
        return _samples.Count / _window.TotalSeconds;
    }

    public void Reset()
    {
        _samples.Clear();
    }

    private void Trim(DateTimeOffset now)
    {
        while (_samples.Count > 0 && now - _samples.Peek() > _window)
        {
            _samples.Dequeue();
        }
    }
}
