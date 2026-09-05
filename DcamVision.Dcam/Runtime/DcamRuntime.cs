using DcamVision.Dcam.Errors;
using DcamVision.Dcam.Interop;
using Microsoft.Extensions.Logging;

namespace DcamVision.Dcam.Runtime;

public sealed class DcamRuntime : IDcamRuntime
{
    private readonly IDcamNativeApi _nativeApi;
    private readonly ILogger<DcamRuntime>? _logger;
    private readonly object _syncRoot = new();
    private bool _initialized;

    public DcamRuntime(IDcamNativeApi nativeApi, ILogger<DcamRuntime>? logger = null)
    {
        _nativeApi = nativeApi;
        _logger = logger;
        Status = _nativeApi.CheckRuntime();
    }

    public DcamRuntimeStatus Status { get; private set; }

    public bool IsInitialized
    {
        get
        {
            lock (_syncRoot)
            {
                return _initialized;
            }
        }
    }

    public DcamRuntimeStatus Initialize()
    {
        lock (_syncRoot)
        {
            if (_initialized)
            {
                return Status;
            }

            Status = _nativeApi.CheckRuntime();
            if (!Status.IsAvailable)
            {
                _logger?.LogInformation("DCAM runtime unavailable: {Reason}", Status.UnavailableReason);
                return Status;
            }

            try
            {
                var result = _nativeApi.Initialize(out var deviceCount);
                DcamErrorTranslator.ThrowIfFailed(result, "dcamapi_init", "Unable to initialize Hamamatsu DCAM runtime.");
                _initialized = true;
                Status = Status with { DeviceCount = deviceCount };
                _logger?.LogInformation("DCAM runtime initialized. Device count: {DeviceCount}.", deviceCount);
                return Status;
            }
            catch (Exception exception) when (exception is DcamException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                Status = DcamRuntimeStatus.Unavailable(exception.Message);
                _logger?.LogWarning(exception, "DCAM runtime initialization failed.");
                return Status;
            }
        }
    }

    public void Uninitialize()
    {
        lock (_syncRoot)
        {
            if (!_initialized)
            {
                return;
            }

            _nativeApi.Uninitialize();
            _initialized = false;
            _logger?.LogInformation("DCAM runtime uninitialized.");
        }
    }

    public void Dispose()
    {
        Uninitialize();
        if (_nativeApi is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
