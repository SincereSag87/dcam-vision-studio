namespace DcamVision.Imaging.Diagnostics;

public interface IApplicationDiagnosticsService
{
    ApplicationDiagnosticsSnapshot CreateSnapshot(DiagnosticsState state);

    string CreateTextReport(ApplicationDiagnosticsSnapshot snapshot, SupportBundleOptions? options = null);
}
