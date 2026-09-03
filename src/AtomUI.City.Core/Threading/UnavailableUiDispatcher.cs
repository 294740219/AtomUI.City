using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Core.Threading;

/// <summary>
/// Represents unavailable ui dispatcher.
/// </summary>
public sealed class UnavailableUiDispatcher : IUiDispatcher
{
    private const string ErrorMessage = $"{HostDiagnosticIds.DispatcherUnavailable}: UI dispatcher is not available. Register an IUiDispatcher implementation from Presentation or Testing before building the application host.";

    /// <summary>
    /// Executes the check access operation.
    /// </summary>
    public bool CheckAccess()
    {
        return false;
    }

    /// <summary>
    /// Returns a failed operation because no UI dispatcher is registered.
    /// </summary>
    public ValueTask InvokeAsync(Action callback)
    {
        return InvokeAsync(callback, CancellationToken.None);
    }

    /// <summary>
    /// Returns a failed operation because no UI dispatcher is registered.
    /// </summary>
    public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        return ValueTask.FromException(CreateException());
    }

    /// <summary>
    /// Returns a failed operation because no UI dispatcher is registered.
    /// </summary>
    public ValueTask<T> InvokeAsync<T>(Func<T> callback)
    {
        return InvokeAsync(callback, CancellationToken.None);
    }

    /// <summary>
    /// Returns a failed operation because no UI dispatcher is registered.
    /// </summary>
    public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<T>(cancellationToken);
        }

        return ValueTask.FromException<T>(CreateException());
    }

    /// <summary>
    /// Executes the post async operation.
    /// </summary>
    public ValueTask PostAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        return ValueTask.FromException(CreateException());
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException(ErrorMessage);
    }
}
