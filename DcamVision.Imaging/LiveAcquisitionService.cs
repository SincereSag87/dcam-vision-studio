using DcamVision.Core;
using Microsoft.Extensions.Logging;

namespace DcamVision.Imaging;

public sealed class LiveAcquisitionService
{
    private readonly ICameraService _cameraService;
    private readonly LiveAcquisitionOptions _options;
    private readonly ILogger<LiveAcquisitionService>? _logger;
    private readonly object _syncRoot = new();
    private readonly RollingFrameRateCalculator _acquisitionRate = new(TimeSpan.FromSeconds(1));
    private readonly RollingFrameRateCalculator _displayRate = new(TimeSpan.FromSeconds(1));
    private CancellationTokenSource? _cancellation;
    private Task? _producerTask;
    private Task? _consumerTask;
    private BoundedFrameBuffer? _buffer;
    private LiveAcquisitionSession? _session;
    private LiveAcquisitionState _state = LiveAcquisitionState.Stopped;
    private ProcessedFrame? _latestProcessedFrame;
    private DateTimeOffset _lastPreviewAt = DateTimeOffset.MinValue;
    private long _framesAcquired;
    private long _framesProcessed;
    private long _framesDisplayed;
    private long _pipelineDrops;
    private long _sourceFrameGaps;
    private long _latestFrameNumber;

    public LiveAcquisitionService(
        ICameraService cameraService,
        LiveAcquisitionOptions? options = null,
        ILogger<LiveAcquisitionService>? logger = null)
    {
        _cameraService = cameraService;
        _options = options ?? new LiveAcquisitionOptions();
        _options.Validate();
        _logger = logger;
        Metrics = LiveAcquisitionMetrics.Empty(_options.BufferCapacity);
    }

    public event EventHandler<ProcessedFrame>? FrameReady;

    public event EventHandler<LiveAcquisitionMetrics>? MetricsUpdated;

    public event EventHandler<LiveAcquisitionState>? StateChanged;

    public event EventHandler<Exception>? Faulted;

    public LiveAcquisitionState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public LiveAcquisitionSession? Session
    {
        get
        {
            lock (_syncRoot)
            {
                return _session;
            }
        }
    }

    public LiveAcquisitionMetrics Metrics { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (_state is not LiveAcquisitionState.Stopped and not LiveAcquisitionState.Faulted)
            {
                throw new InvalidOperationException("Live acquisition is already running.");
            }

            if (_cameraService.ConnectedDevice is null)
            {
                throw new InvalidOperationException("Connect to a camera before starting live acquisition.");
            }

            ResetMetrics();
            _session = new LiveAcquisitionSession();
            _buffer = new BoundedFrameBuffer(_options.BufferCapacity, _options.OverflowStrategy);
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lastPreviewAt = DateTimeOffset.MinValue;
            _latestProcessedFrame = null;
            _state = LiveAcquisitionState.Starting;
        }

        OnStateChanged(LiveAcquisitionState.Starting);
        _logger?.LogInformation(
            "Live acquisition session started. Buffer capacity: {Capacity}. Overflow: {Overflow}. Preview FPS limit: {PreviewFps}.",
            _options.BufferCapacity,
            _options.OverflowStrategy,
            _options.MaximumPreviewFps);

        var token = _cancellation.Token;
        _producerTask = Task.Run(() => RunProducerAsync(token), CancellationToken.None);
        _consumerTask = Task.Run(() => RunConsumerAsync(token), CancellationToken.None);

        SetState(LiveAcquisitionState.Running);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? producer;
        Task? consumer;
        CancellationTokenSource? cancellation;

        lock (_syncRoot)
        {
            if (_state is LiveAcquisitionState.Stopped)
            {
                return;
            }

            _state = LiveAcquisitionState.Stopping;
            producer = _producerTask;
            consumer = _consumerTask;
            cancellation = _cancellation;
        }

        OnStateChanged(LiveAcquisitionState.Stopping);
        cancellation?.Cancel();

        try
        {
            var tasks = new[] { producer, consumer }.Where(task => task is not null).Cast<Task>().ToArray();
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation?.Dispose();
            lock (_syncRoot)
            {
                _session = _session is null ? null : _session with { StoppedAt = DateTimeOffset.UtcNow };
                _producerTask = null;
                _consumerTask = null;
                _cancellation = null;
                _buffer = null;
                _state = LiveAcquisitionState.Stopped;
            }

            _logger?.LogInformation(
                "Live acquisition stopped. Frames acquired: {Acquired}. Frames displayed: {Displayed}. Frames dropped: {Dropped}.",
                Metrics.FramesAcquired,
                Metrics.FramesDisplayed,
                Metrics.TotalDropped);
            OnStateChanged(LiveAcquisitionState.Stopped);
            PublishMetrics();
        }
    }

    public void PausePreview()
    {
        if (State is not LiveAcquisitionState.Running)
        {
            return;
        }

        SetState(LiveAcquisitionState.PreviewPaused);
        _logger?.LogInformation("Live preview paused.");
    }

    public void ResumePreview()
    {
        if (State is not LiveAcquisitionState.PreviewPaused)
        {
            return;
        }

        SetState(LiveAcquisitionState.Running);
        _logger?.LogInformation("Live preview resumed.");

        var latest = _latestProcessedFrame;
        if (latest is not null)
        {
            PublishFrame(latest);
        }
    }

    private async Task RunProducerAsync(CancellationToken cancellationToken)
    {
        var previousFrameNumber = 0L;
        try
        {
            await foreach (var frame in _cameraService.StreamFramesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (previousFrameNumber > 0 && frame.FrameNumber > previousFrameNumber + 1)
                {
                    Interlocked.Add(ref _sourceFrameGaps, frame.FrameNumber - previousFrameNumber - 1);
                }

                previousFrameNumber = frame.FrameNumber;
                Interlocked.Increment(ref _framesAcquired);
                Interlocked.Exchange(ref _latestFrameNumber, frame.FrameNumber);
                _acquisitionRate.Record(DateTimeOffset.UtcNow);

                var buffer = _buffer;
                if (buffer is null)
                {
                    return;
                }

                var dropped = await buffer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                if (dropped > 0)
                {
                    Interlocked.Add(ref _pipelineDrops, dropped);
                }

                PublishMetrics();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
        finally
        {
            _buffer?.Complete();
        }
    }

    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var buffer = _buffer;
                if (buffer is null)
                {
                    return;
                }

                var frame = await buffer.ReadLatestAsync(cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    return;
                }

                var histogram = HistogramAnalyzer.Analyze(frame, bins: 256);
                var processed = new ProcessedFrame(
                    frame,
                    new FrameStatistics(
                        histogram.Minimum,
                        histogram.Maximum,
                        histogram.Mean,
                        frame.Width,
                        frame.Height,
                        frame.FrameNumber,
                        histogram.SaturatedPixelCount,
                        histogram.SaturationPercentage),
                    histogram);

                _latestProcessedFrame = processed;
                Interlocked.Increment(ref _framesProcessed);

                if (State is LiveAcquisitionState.PreviewPaused)
                {
                    PublishMetrics();
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                if (now - _lastPreviewAt >= _options.MinimumPreviewInterval)
                {
                    _lastPreviewAt = now;
                    PublishFrame(processed);
                }
                else
                {
                    PublishMetrics();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
    }

    private void PublishFrame(ProcessedFrame frame)
    {
        Interlocked.Increment(ref _framesDisplayed);
        _displayRate.Record(DateTimeOffset.UtcNow);
        FrameReady?.Invoke(this, frame);
        PublishMetrics();
    }

    private void HandleFault(Exception exception)
    {
        _logger?.LogError(exception, "Live acquisition failed.");
        SetState(LiveAcquisitionState.Faulted);
        Faulted?.Invoke(this, exception);
        _cancellation?.Cancel();
    }

    private void SetState(LiveAcquisitionState state)
    {
        lock (_syncRoot)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        OnStateChanged(state);
        PublishMetrics();
    }

    private void OnStateChanged(LiveAcquisitionState state)
    {
        StateChanged?.Invoke(this, state);
    }

    private void ResetMetrics()
    {
        _framesAcquired = 0;
        _framesProcessed = 0;
        _framesDisplayed = 0;
        _pipelineDrops = 0;
        _sourceFrameGaps = 0;
        _latestFrameNumber = 0;
        _acquisitionRate.Reset();
        _displayRate.Reset();
        Metrics = LiveAcquisitionMetrics.Empty(_options.BufferCapacity);
    }

    private void PublishMetrics()
    {
        var session = Session;
        var now = DateTimeOffset.UtcNow;
        var elapsed = session is null ? TimeSpan.Zero : now - session.StartedAt;
        var buffer = _buffer;

        var metrics = new LiveAcquisitionMetrics(
            Interlocked.Read(ref _framesAcquired),
            Interlocked.Read(ref _framesProcessed),
            Interlocked.Read(ref _framesDisplayed),
            Interlocked.Read(ref _pipelineDrops),
            Interlocked.Read(ref _sourceFrameGaps),
            _acquisitionRate.FramesPerSecond(now),
            _displayRate.FramesPerSecond(now),
            elapsed,
            Interlocked.Read(ref _latestFrameNumber),
            Math.Max(0, Math.Min(_options.BufferCapacity, buffer?.Count ?? 0)),
            _options.BufferCapacity);

        Metrics = metrics;
        MetricsUpdated?.Invoke(this, metrics);
    }
}
