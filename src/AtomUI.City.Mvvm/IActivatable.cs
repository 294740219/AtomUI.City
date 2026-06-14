namespace AtomUI.City.Mvvm;

public interface IActivatable
{
    ValueTask ActivateAsync(IActivationScope scope);

    ValueTask ActivateAsync(IActivationScope scope, CancellationToken cancellationToken);

    ValueTask DeactivateAsync();

    ValueTask DeactivateAsync(CancellationToken cancellationToken);
}
