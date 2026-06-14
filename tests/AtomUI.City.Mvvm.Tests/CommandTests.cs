using AtomUI.City.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace AtomUI.City.Mvvm.Tests;

public sealed class CommandTests
{
    [Fact]
    public void CreateCommandUsesCommunityToolkitRelayCommand()
    {
        var calls = 0;
        var command = CommandFactory.Create(() => calls++);

        Assert.IsAssignableFrom<IRelayCommand>(command);

        command.Execute(null);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void SyncCommandCapturesFailureWithoutThrowing()
    {
        var exception = new InvalidOperationException("boom");
        var state = new CommandExecutionState("save", typeof(SaveViewModel));
        var command = CommandFactory.Create(
            () => throw exception,
            state: state);

        command.Execute(null);

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Failed, state.LastResult?.Status);
        Assert.Same(exception, state.LastError);
        Assert.Same(exception, state.LastResult?.Error);
        Assert.NotEqual(Guid.Empty, state.LastResult?.OperationId);
        Assert.Equal("save", state.CommandName);
        Assert.Equal(typeof(SaveViewModel), state.OwnerType);
    }

    [Fact]
    public void SyncCommandNotifiesCanExecuteChanges()
    {
        var enabled = false;
        var changes = 0;
        var state = new CommandExecutionState("refresh", typeof(SaveViewModel));
        var command = CommandFactory.Create(
            () => { },
            canExecute: () => enabled,
            state: state);

        command.CanExecuteChanged += (_, _) => changes++;

        Assert.False(command.CanExecute(null));

        enabled = true;
        command.NotifyCanExecuteChanged();

        Assert.True(command.CanExecute(null));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task AsyncCommandTracksSuccessfulExecution()
    {
        var state = new CommandExecutionState();
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            },
            state);

        Assert.IsAssignableFrom<IAsyncRelayCommand>(command);

        await command.ExecuteAsync(null);

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task AsyncCommandRejectsConcurrentExecution()
    {
        var state = new CommandExecutionState("load", typeof(SaveViewModel));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                executions++;
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            },
            state);

        var first = command.ExecuteAsync(null);
        await started.Task;

        var second = command.ExecuteAsync(null);
        await second;

        Assert.True(state.IsExecuting);
        Assert.Equal(1, executions);
        Assert.Equal(1, state.RejectedExecutionCount);
        Assert.Equal(OperationStatus.Rejected, state.LastRejectedResult?.Status);
        Assert.NotEqual(Guid.Empty, state.LastRejectedResult?.OperationId);

        release.SetResult();
        await first;

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Completed, state.LastResult?.Status);
    }

    [Fact]
    public async Task AsyncCommandCapturesFailureWithoutThrowing()
    {
        var state = new CommandExecutionState();
        var command = CommandFactory.CreateAsync(
            _ => throw new InvalidOperationException("boom"),
            state);

        await command.ExecuteAsync(null);

        Assert.False(state.IsExecuting);
        Assert.Equal(OperationStatus.Failed, state.LastResult?.Status);
        Assert.IsType<InvalidOperationException>(state.LastError);
    }

    [Fact]
    public async Task AsyncCommandIsCanceledWhenActivationScopeStops()
    {
        await using var scope = new ActivationScope();
        var state = new CommandExecutionState();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            },
            state,
            scope);

        var execution = command.ExecuteAsync(null);
        await started.Task;

        await scope.DisposeAsync();
        await execution;

        Assert.Equal(OperationStatus.Canceled, state.LastResult?.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public void CommandGroupExecutesOnlyActiveCommands()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var group = new CommandGroup();

        group.Register(CommandFactory.Create(() => firstCalls++), isActive: () => true);
        group.Register(CommandFactory.Create(() => secondCalls++), isActive: () => false);

        group.Execute(null);

        Assert.Equal(1, firstCalls);
        Assert.Equal(0, secondCalls);
    }

    [Fact]
    public void CommandGroupRegistrationIsRemovedWithActivationScope()
    {
        var calls = 0;
        var scope = new ActivationScope();
        var group = new CommandGroup();

        group.Register(CommandFactory.Create(() => calls++), activationScope: scope);
        scope.Dispose();
        group.Execute(null);

        Assert.Equal(0, calls);
    }

    private sealed class SaveViewModel;
}
