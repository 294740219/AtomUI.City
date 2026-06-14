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
        ArgumentNullException.ThrowIfNull(scope);

        await ActivateAsync(new ActivationContext(scope)).ConfigureAwait(false);
    }

    public async ValueTask ActivateAsync(ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();

        ActivationState = ActivationState.Activating;
        ActivationContext = context;

        await OnActivatedAsync(context).ConfigureAwait(false);

        ActivationState = ActivationState.Active;
    }

    public async ValueTask DeactivateAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (ActivationState is ActivationState.Deactivated or ActivationState.Constructed)
        {
            return;
        }

        ActivationState = ActivationState.Deactivating;

        try
        {
            await OnDeactivatedAsync().ConfigureAwait(false);
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

    protected virtual ValueTask OnActivatedAsync(IActivationScope scope) => ValueTask.CompletedTask;

    protected virtual ValueTask OnDeactivatedAsync() => ValueTask.CompletedTask;

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
}
