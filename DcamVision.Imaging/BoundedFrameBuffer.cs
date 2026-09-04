using System.Threading.Channels;
using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed class BoundedFrameBuffer
{
    private readonly Queue<CameraFrame> _frames = new();
    private readonly Channel<bool> _notifications = Channel.CreateUnbounded<bool>();
    private readonly object _syncRoot = new();
    private bool _completed;

    public BoundedFrameBuffer(int capacity, LiveBufferOverflowStrategy overflowStrategy)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Buffer capacity must be at least 1.");
        }

        Capacity = capacity;
        OverflowStrategy = overflowStrategy;
    }

    public int Capacity { get; }

    public LiveBufferOverflowStrategy OverflowStrategy { get; }

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _frames.Count;
            }
        }
    }

    public async ValueTask<int> WriteAsync(CameraFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                if (_completed)
                {
                    throw new ChannelClosedException();
                }

                if (_frames.Count < Capacity)
                {
                    _frames.Enqueue(frame);
                    SignalReader();
                    return 0;
                }

                if (OverflowStrategy == LiveBufferOverflowStrategy.DropNewest)
                {
                    return 1;
                }

                if (OverflowStrategy == LiveBufferOverflowStrategy.DropOldest)
                {
                    _frames.Dequeue();
                    _frames.Enqueue(frame);
                    SignalReader();
                    return 1;
                }
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<CameraFrame?> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            lock (_syncRoot)
            {
                if (_frames.Count > 0)
                {
                    CameraFrame latest = _frames.Dequeue();
                    while (_frames.Count > 0)
                    {
                        latest = _frames.Dequeue();
                    }

                    return latest;
                }

                if (_completed)
                {
                    return null;
                }
            }

            try
            {
                await _notifications.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }
    }

    public void Complete(Exception? exception = null)
    {
        lock (_syncRoot)
        {
            _completed = true;
        }

        if (exception is null)
        {
            _notifications.Writer.TryComplete();
        }
        else
        {
            _notifications.Writer.TryComplete(exception);
        }
    }

    private void SignalReader()
    {
        _notifications.Writer.TryWrite(true);
    }
}
