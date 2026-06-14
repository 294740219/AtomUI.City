using System.Collections.ObjectModel;

namespace AtomUI.City.Mvvm;

public sealed class ValidationScope : IDisposable
{
    private readonly Dictionary<string, IReadOnlyList<string>> _errors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ValidationMessage>> _messages = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, IReadOnlyList<string>> _readOnlyErrors;
    private readonly ReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>> _readOnlyMessages;
    private Guid? _ownerScopeId;
    private bool _disposed;

    public ValidationScope()
    {
        _readOnlyErrors = new ReadOnlyDictionary<string, IReadOnlyList<string>>(_errors);
        _readOnlyMessages = new ReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>>(_messages);
    }

    public event EventHandler<ValidationChangedEventArgs>? ValidationChanged;

    public ValidationStatus Status { get; private set; } = ValidationStatus.Valid;

    public bool IsDisposed => _disposed;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors => _readOnlyErrors;

    public IReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>> Messages => _readOnlyMessages;

    public Exception? Exception { get; private set; }

    public void BindTo(IActivationScope activationScope)
    {
        ArgumentNullException.ThrowIfNull(activationScope);
        ThrowIfDisposed();

        _ownerScopeId = activationScope.Id;
        activationScope.Add(new DelegateDisposable(Cancel));
    }

    public void SetInvalid(
        string key,
        string message,
        string? messageKey = null,
        IReadOnlyList<object?>? messageArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        SetMessages(
            key,
            [
                new ValidationMessage(
                    key,
                    message,
                    messageKey,
                    messageArguments),
            ]);
    }

    public void SetMessages(
        string? key,
        IEnumerable<ValidationMessage?> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ThrowIfDisposed();

        var normalizedKey = NormalizeKey(key);
        var uniqueMessages = new List<ValidationMessage>();
        var seen = new HashSet<(string Message, string? MessageKey)>();

        foreach (var message in messages)
        {
            if (message is null)
            {
                throw new ArgumentException("Validation messages cannot contain null.", nameof(messages));
            }

            if (seen.Add((message.Message, message.MessageKey)))
            {
                uniqueMessages.Add(message);
            }
        }

        Exception = null;

        if (uniqueMessages.Count == 0)
        {
            _errors.Remove(normalizedKey);
            _messages.Remove(normalizedKey);
        }
        else
        {
            _errors[normalizedKey] = Array.AsReadOnly(uniqueMessages.Select(message => message.Message).ToArray());
            _messages[normalizedKey] = Array.AsReadOnly(uniqueMessages.ToArray());
        }

        Status = _messages.Count == 0
            ? ValidationStatus.Valid
            : ValidationStatus.Invalid;
        RaiseChanged(normalizedKey);
    }

    public void SetPending()
    {
        ThrowIfDisposed();

        Exception = null;
        Status = ValidationStatus.Pending;
        RaiseChanged(string.Empty);
    }

    public void SetFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ThrowIfDisposed();

        _errors.Clear();
        _messages.Clear();
        Exception = exception;
        Status = ValidationStatus.Failed;
        RaiseChanged(string.Empty);
    }

    public void Cancel()
    {
        if (_disposed)
        {
            return;
        }

        Exception = null;
        Status = ValidationStatus.Canceled;
        RaiseChanged(string.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    private static string NormalizeKey(string? key)
    {
        return key is null ? string.Empty : key;
    }

    private void RaiseChanged(string key)
    {
        _errors.TryGetValue(key, out var errors);
        _messages.TryGetValue(key, out var messages);

        ValidationChanged?.Invoke(
            this,
            new ValidationChangedEventArgs(
                key,
                Status,
                errors ?? Array.Empty<string>(),
                messages ?? Array.Empty<ValidationMessage>(),
                _ownerScopeId));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
