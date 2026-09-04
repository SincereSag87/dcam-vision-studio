namespace DcamVision.Imaging;

public sealed record ImageDisplaySettings
{
    public ushort BlackPoint { get; init; } = ushort.MinValue;

    public ushort WhitePoint { get; init; } = ushort.MaxValue;

    public double Gamma { get; init; } = 1.0;

    public bool Invert { get; init; }

    public bool ThresholdEnabled { get; init; }

    public ushort ThresholdValue { get; init; } = ushort.MaxValue / 2;

    public AutoContrastMode AutoContrastMode { get; init; } = AutoContrastMode.Off;

    public double LowerPercentile { get; init; } = 1.0;

    public double UpperPercentile { get; init; } = 99.0;

    public HistogramScale HistogramScale { get; init; } = HistogramScale.Linear;

    public HistogramDisplayRange HistogramDisplayRange { get; init; } = HistogramDisplayRange.Full;

    public static ImageDisplaySettings Default { get; } = new();

    public void Validate()
    {
        if (WhitePoint <= BlackPoint)
        {
            throw new ArgumentException("White point must be greater than black point.");
        }

        if (Gamma is <= 0 or < 0.1 or > 5.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Gamma), "Gamma must be between 0.1 and 5.0.");
        }

        if (LowerPercentile < 0 || LowerPercentile >= UpperPercentile)
        {
            throw new ArgumentOutOfRangeException(nameof(LowerPercentile), "Lower percentile must be less than upper percentile.");
        }

        if (UpperPercentile > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(UpperPercentile), "Upper percentile must be no greater than 100.");
        }
    }

    public ImageDisplaySettings ApplyPreset(DisplayPreset preset)
    {
        return preset switch
        {
            DisplayPreset.Raw => Default,
            DisplayPreset.AutoMinMax => Default with { AutoContrastMode = AutoContrastMode.MinMax },
            DisplayPreset.AutoPercentile => Default with { AutoContrastMode = AutoContrastMode.Percentile },
            DisplayPreset.HighContrast => Default with { AutoContrastMode = AutoContrastMode.Percentile, LowerPercentile = 5.0, UpperPercentile = 95.0, Gamma = 1.1 },
            DisplayPreset.LowContrast => Default with { AutoContrastMode = AutoContrastMode.Off, BlackPoint = 0, WhitePoint = ushort.MaxValue, Gamma = 0.8 },
            _ => Default
        };
    }
}
