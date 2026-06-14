using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtomUI.City.Mvvm;

public abstract class ViewModelBase : ObservableValidator, IActivatable, IDisposable
{
    private bool _disposed;

    public ActivationState ActivationState { get; private set; } = ActivationState.Constructed;

    public bool IsActive => ActivationState == ActivationState.Active;

    public bool IsDisposed => _disposed;

    public IActivationScope? CurrentActivationScope => ActivationContext?.Scope;

    public ActivationContext? ActivationContext { get; private set; }

    public async ValueTask ActivateAsync(IActivationScope scope)
    {
        await ActivateAsync(scope, CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await ActivateAsync(new ActivationContext(scope), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ActivateAsync(ActivationContext context)
    {
        await ActivateAsync(context, CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask ActivateAsync(ActivationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();

        if (ActivationState == ActivationState.Active)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            AbortActivation(context);
            cancellationToken.ThrowIfCancellationRequested();
        }

        ActivationState = ActivationState.Activating;
        ActivationContext = context;

        try
        {
            context.Scope.CancellationToken.ThrowIfCancellationRequested();

            await OnActivatedAsync(context, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            context.Scope.CancellationToken.ThrowIfCancellationRequested();
            ActivationState = ActivationState.Active;
        }
        catch (OperationCanceledException)
        {
            AbortActivation(context);
            throw;
        }
        catch (Exception exception)
        {
            EnrichActivationException(exception, context, "Activating");
            AbortActivation(context);
            throw;
        }
    }

    public async ValueTask DeactivateAsync()
    {
        await DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (ActivationState is ActivationState.Deactivated or ActivationState.Constructed)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        ActivationState = ActivationState.Deactivating;

        try
        {
            await OnDeactivatedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ActivationContext?.Scope.Dispose();
            ActivationContext = null;
            ActivationState = ActivationState.Deactivated;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            ActivationContext?.Scope.Dispose();
        }
        finally
        {
            ActivationContext = null;
            ActivationState = ActivationState.Disposed;
            OnDisposed();
            GC.SuppressFinalize(this);
        }
    }

    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(ref field, newValue, EqualityComparer<T>.Default, propertyName);
    }

    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        IEqualityComparer<T> comparer,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, comparer, propertyName);
    }

    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, validate, propertyName);
    }

    protected new bool SetProperty<T>(
        ref T field,
        T newValue,
        IEqualityComparer<T> comparer,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return base.SetProperty(ref field, newValue, comparer, validate, propertyName);
    }

    protected virtual ValueTask OnActivatedAsync(ActivationContext context) => OnActivatedAsync(context.Scope);

    protected virtual ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnActivatedAsync(context);
    }

    protected virtual ValueTask OnActivatedAsync(IActivationScope scope) => ValueTask.CompletedTask;

    protected virtual ValueTask OnDeactivatedAsync() => ValueTask.CompletedTask;

    protected virtual ValueTask OnDeactivatedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnDeactivatedAsync();
    }

    protected virtual void OnDisposed()
    {
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private void AbortActivation(ActivationContext context)
    {
        ActivationContext = null;
        context.Scope.Dispose();
        ActivationState = ActivationState.Deactivated;
    }

    private void EnrichActivationException(
        Exception exception,
        ActivationContext context,
        string stage)
    {
        AddExceptionData(exception, "AtomUI.City.Mvvm.ViewModelType", GetType().FullName);
        AddExceptionData(exception, "AtomUI.City.Mvvm.ActivationStage", stage);
        AddExceptionData(exception, "AtomUI.City.Mvvm.ScopeId", context.Scope.Id);
    }

    private static void AddExceptionData(Exception exception, string key, object? value)
    {
        if (exception.Data.Contains(key))
        {
            return;
        }

        exception.Data[key] = value;
    }
}
