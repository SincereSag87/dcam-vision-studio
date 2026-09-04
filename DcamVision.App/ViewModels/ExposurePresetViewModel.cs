using DcamVision.Core;

namespace DcamVision.App.ViewModels;

public sealed class ExposurePresetViewModel : ObservableObject
{
    private bool _isActive;
    private bool _isSupported;

    public ExposurePresetViewModel(ExposurePreset preset, CameraExposureRange range)
    {
        Preset = preset;
        _isSupported = range.Contains(preset.Exposure);
    }

    public ExposurePreset Preset { get; }

    public string DisplayName => Preset.DisplayName;

    public TimeSpan Exposure => Preset.Exposure;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsSupported
    {
        get => _isSupported;
        set => SetProperty(ref _isSupported, value);
    }
}
