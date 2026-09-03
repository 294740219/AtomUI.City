namespace AtomUI.City.Core.Threading;

/// <summary>
/// Defines the contract for iui dispatcher.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Executes the check access operation.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Invokes a callback on the UI thread.
    /// </summary>
    ValueTask InvokeAsync(Action callback) => InvokeAsync(callback, CancellationToken.None);

    /// <summary>
    /// Invokes a callback on the UI thread and observes cancellation while it is pending.
    /// </summary>
    ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes a value-returning callback on the UI thread.
    /// </summary>
    ValueTask<T> InvokeAsync<T>(Func<T> callback) => InvokeAsync(callback, CancellationToken.None);

    /// <summary>
    /// Invokes a value-returning callback on the UI thread and observes cancellation while it is pending.
    /// </summary>
    ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the post async operation.
    /// </summary>
    ValueTask PostAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default);
}
