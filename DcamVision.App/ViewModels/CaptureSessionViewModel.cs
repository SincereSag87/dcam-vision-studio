using DcamVision.Imaging;

namespace DcamVision.App.ViewModels;

public sealed record CaptureSessionViewModel(Guid? SessionId, string Name)
{
    public static CaptureSessionViewModel All { get; } = new(null, "All Sessions");

    public static CaptureSessionViewModel FromSession(CaptureSession session)
    {
        return new CaptureSessionViewModel(session.SessionId, session.Name);
    }
}
