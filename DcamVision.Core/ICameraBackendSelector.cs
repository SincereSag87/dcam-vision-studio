namespace DcamVision.Core;

public interface ICameraBackendSelector
{
    CameraBackend SelectedBackend { get; set; }

    IReadOnlyList<CameraBackend> AvailableBackends { get; }
}
