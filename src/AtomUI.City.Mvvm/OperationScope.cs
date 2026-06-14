using System.Diagnostics;

namespace AtomUI.City.Mvvm;

public sealed class OperationScope : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellationSource = new();
    private readonly CancellationToken _cancellationToken;
    private readonly Stopwatch _stopwatch;
    private CancellationTokenRegistration _externalCancellation;
    private OperationStatus _status = OperationStatus.Running;
    private OperationResult? _result;
    private Exception? _error;
    private bool _isDisposed;

    private OperationScope(CancellationToken cancellationToken)
    {
        Id = Guid.NewGuid();
        _cancellationToken = _cancellationSource.Token;
        _stopwatch = Stopwatch.StartNew();

        if (cancellationToken.CanBeCanceled)
        {
            _externalCancellation = cancellationToken.Register(
                static state => ((OperationScope)state!).CancelFromExternal(),
                this);
        }
    }

    public Guid Id { get; }

    public CancellationToken CancellationToken => _cancellationToken;

    public OperationStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public OperationResult? Result
    {
        get
        {
            lock (_gate)
            {
                return _result;
            }
        }
    }

    public Exception? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_gate)
            {
                return _result?.Elapsed ?? _stopwatch.Elapsed;
            }
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _isDisposed;
            }
        }
    }

    public static OperationScope Start(CancellationToken cancellationToken)
    {
        return new OperationScope(cancellationToken);
    }

    public OperationResult Complete()
    {
        return Finish(OperationStatus.Completed, null, requestCancellation: false, throwIfDisposed: true);
    }

    public OperationResult Cancel()
    {
        return Finish(OperationStatus.Canceled, null, requestCancellation: true, throwIfDisposed: true);
    }

    public OperationResult Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Finish(OperationStatus.Failed, exception, requestCancellation: false, throwIfDisposed: true);
    }

    public OperationResult Reject()
    {
        return Finish(OperationStatus.Rejected, null, requestCancellation: false, throwIfDisposed: true);
    }

    public void Dispose()
    {
        var shouldCancel = false;

        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_result is null)
            {
                _stopwatch.Stop();
                _status = OperationStatus.Canceled;
                _result = new OperationResult(Id, OperationStatus.Canceled, _stopwatch.Elapsed);
                shouldCancel = !_cancellationSource.IsCancellationRequested;
            }

            _isDisposed = true;
        }

        if (shouldCancel)
        {
            _cancellationSource.Cancel();
        }

        _externalCancellation.Dispose();
        _cancellationSource.Dispose();
    }

    private OperationResult Finish(
        OperationStatus status,
        Exception? error,
        bool requestCancellation,
        bool throwIfDisposed)
    {
        var shouldCancel = false;
        OperationResult result;

        lock (_gate)
        {
            if (_isDisposed && throwIfDisposed)
            {
                throw new ObjectDisposedException(nameof(OperationScope));
            }

            if (_result is not null)
            {
                return _result;
            }

            _stopwatch.Stop();
            _status = status;
            _error = error;
            _result = new OperationResult(Id, status, _stopwatch.Elapsed, error);
            result = _result;
            shouldCancel = requestCancellation && !_cancellationSource.IsCancellationRequested;
        }

        if (shouldCancel)
        {
            _cancellationSource.Cancel();
        }

        return result;
    }

    private void CancelFromExternal()
    {
        Finish(OperationStatus.Canceled, null, requestCancellation: true, throwIfDisposed: false);
    }
}
