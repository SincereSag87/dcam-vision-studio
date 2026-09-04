namespace DcamVision.Core;

public sealed record ExposurePreset(string DisplayName, TimeSpan Exposure)
{
    public static IReadOnlyList<ExposurePreset> Defaults { get; } =
    [
        new("100 µs", TimeSpan.FromMicroseconds(100)),
        new("500 µs", TimeSpan.FromMicroseconds(500)),
        new("1 ms", TimeSpan.FromMilliseconds(1)),
        new("5 ms", TimeSpan.FromMilliseconds(5)),
        new("10 ms", TimeSpan.FromMilliseconds(10)),
        new("25 ms", TimeSpan.FromMilliseconds(25)),
        new("50 ms", TimeSpan.FromMilliseconds(50)),
        new("100 ms", TimeSpan.FromMilliseconds(100)),
        new("500 ms", TimeSpan.FromMilliseconds(500)),
        new("1 s", TimeSpan.FromSeconds(1))
    ];
}
