namespace AtomUI.City.Mvvm;

public interface IActivationScope : IDisposable, IAsyncDisposable
{
    Guid Id { get; }

    CancellationToken CancellationToken { get; }

    void Add(IDisposable disposable);

    void AddAsync(IAsyncDisposable disposable);
}
