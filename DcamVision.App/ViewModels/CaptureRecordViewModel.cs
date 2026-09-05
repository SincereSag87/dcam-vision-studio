using System.Globalization;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.App.ViewModels;

public sealed class CaptureRecordViewModel : ObservableObject
{
    private bool _isCompareA;
    private bool _isCompareB;

    public CaptureRecordViewModel(CaptureRecord record, CaptureSession? session)
    {
        Record = record;
        Session = session;
    }

    public CaptureRecord Record { get; }

    public CaptureSession? Session { get; }

    public Guid CaptureId => Record.CaptureId;

    public string DisplayName => $"#{Record.SequenceNumber}  {ExposureUnitConverter.Format(Record.Metadata.Exposure)}  {Record.Source}";

    public string DetailText => $"{Record.Frame.Width} x {Record.Frame.Height} {Record.Frame.PixelFormat} | {Record.CapturedAt.LocalDateTime:HH:mm:ss}";

    public string SessionName => Session?.Name ?? "Unassigned";

    public string SourceText => Record.Source.ToString();

    public string TimestampText => Record.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string ExposureText => ExposureUnitConverter.Format(Record.Metadata.Exposure);

    public string MeanText => Record.Statistics.Mean.ToString("0.0", CultureInfo.InvariantCulture);

    public string MinMaxText => $"{Record.Statistics.Minimum} / {Record.Statistics.Maximum}";

    public string SaturationText => $"{Record.Statistics.SaturationPercentage:0.0}%";

    public string TagsText => Record.Tags.Count == 0 ? "No tags" : string.Join(", ", Record.Tags);

    public string NotesIndicator => string.IsNullOrWhiteSpace(Record.Notes) ? string.Empty : "Notes";

    public bool IsCompareA
    {
        get => _isCompareA;
        set => SetProperty(ref _isCompareA, value);
    }

    public bool IsCompareB
    {
        get => _isCompareB;
        set => SetProperty(ref _isCompareB, value);
    }
}
