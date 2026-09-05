namespace DcamVision.Dcam.Runtime;

public interface IDcamRuntime : IDisposable
{
    DcamRuntimeStatus Status { get; }

    bool IsInitialized { get; }

    DcamRuntimeStatus Initialize();

    void Uninitialize();
}
