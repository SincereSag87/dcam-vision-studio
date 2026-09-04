using System.Globalization;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.App.ViewModels;

public sealed class SweepResultFrameViewModel
{
    public SweepResultFrameViewModel(int index, ExposureSweepFrame result)
    {
        Index = index;
        Result = result;
    }

    public int Index { get; }

    public ExposureSweepFrame Result { get; }

    public string DisplayName => $"{Index + 1}. {ExposureUnitConverter.Format(Result.Exposure)}";

    public string ExposureText => ExposureUnitConverter.Format(Result.Exposure);

    public string FrameNumberText => Result.Frame.FrameNumber.ToString(CultureInfo.InvariantCulture);

    public string MeanText => Result.Statistics.Mean.ToString("0.0", CultureInfo.InvariantCulture);

    public string MinimumText => Result.Statistics.Minimum.ToString(CultureInfo.InvariantCulture);

    public string MaximumText => Result.Statistics.Maximum.ToString(CultureInfo.InvariantCulture);

    public string SaturationText => $"{Result.Statistics.SaturationPercentage:0.0}%";

    public string SaturationState => Result.Statistics.SaturationPercentage >= 10 ? "High Saturation" : "Normal";
}
