using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Mvvm;

public sealed class CommandGroup : IRelayCommand
{
    private readonly object _gate = new();
    private readonly List<CommandRegistration> _registrations = [];

    public event EventHandler? CanExecuteChanged;

    public IDisposable Register(
        ICommand command,
        Func<bool>? isActive = null,
        IActivationScope? activationScope = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        var registration = new CommandRegistration(this, command, isActive ?? (() => true));

        lock (_gate)
        {
            _registrations.Add(registration);
        }

        activationScope?.Add(registration);
        NotifyCanExecuteChanged();

        return registration;
    }

    public bool CanExecute(object? parameter)
    {
        lock (_gate)
        {
            return _registrations.Any(registration =>
                registration.IsActive() &&
                registration.Command.CanExecute(parameter));
        }
    }

    public void Execute(object? parameter)
    {
        using var operation = OperationScope.Start(CancellationToken.None);
        CommandRegistration[] registrations;

        lock (_gate)
        {
            registrations = _registrations.ToArray();
        }

        try
        {
            foreach (var registration in registrations)
            {
                if (registration.IsActive() && registration.Command.CanExecute(parameter))
                {
                    registration.Command.Execute(parameter);
                }
            }

            operation.Complete();
        }
        catch (Exception exception)
        {
            operation.Fail(exception);
            throw;
        }
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Remove(CommandRegistration registration)
    {
        lock (_gate)
        {
            _registrations.Remove(registration);
        }

        NotifyCanExecuteChanged();
    }

    private sealed class CommandRegistration : IDisposable
    {
        private readonly CommandGroup _group;
        private bool _disposed;

        public CommandRegistration(
            CommandGroup group,
            ICommand command,
            Func<bool> isActive)
        {
            _group = group;
            Command = command;
            IsActive = isActive;
            command.CanExecuteChanged += OnCommandCanExecuteChanged;
        }

        public ICommand Command { get; }

        public Func<bool> IsActive { get; }

        private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
        {
            _group.NotifyCanExecuteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Command.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _group.Remove(this);
        }
    }
}
