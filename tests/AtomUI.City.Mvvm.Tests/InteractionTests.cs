using AtomUI.City.Mvvm;

namespace AtomUI.City.Mvvm.Tests;

public sealed class InteractionTests
{
    [Fact]
    public async Task RequestAsyncReturnsNotHandledWhenNoHandlerExists()
    {
        var interaction = new Interaction<ConfirmRequest, bool>();

        var result = await interaction.RequestAsync(new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.NotHandled, result.Status);
    }

    [Fact]
    public async Task RequestAsyncReturnsCompletedResultFromRegisteredHandler()
    {
        using var scope = new ActivationScope();
        var interaction = new Interaction<ConfirmRequest, bool>();
        interaction.RegisterHandler(
            (context, _) => ValueTask.FromResult(context.Request.Message == "Delete?"),
            scope);

        var result = await interaction.RequestAsync(new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.Completed, result.Status);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task RequestAsyncSupportsGenericResultContracts()
    {
        using var scope = new ActivationScope();
        var interaction = new Interaction<ConfirmRequest, ConfirmResult>();

        interaction.RegisterHandler(
            (context, _) => ValueTask.FromResult(new ConfirmResult(context.Request.Message, true)),
            scope);

        var result = await interaction.RequestAsync(new ConfirmRequest("Archive?"));

        Assert.Equal(InteractionResultStatus.Completed, result.Status);
        Assert.Equal(new ConfirmResult("Archive?", true), result.Value);
    }

    [Fact]
    public async Task RequestAsyncMapsHandlerExceptionToFailedResult()
    {
        using var scope = new ActivationScope();
        var exception = new InvalidOperationException("dialog failed");
        var interaction = new Interaction<ConfirmRequest, bool>();

        interaction.RegisterHandler(
            (_, _) => throw exception,
            scope);

        var result = await interaction.RequestAsync(new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.Failed, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task RequestAsyncDoesNotCommitHandlerResultAfterCancellation()
    {
        using var scope = new ActivationScope();
        using var cancellation = new CancellationTokenSource();
        var interaction = new Interaction<ConfirmRequest, bool>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        interaction.RegisterHandler(
            async (_, _) =>
            {
                started.SetResult();
                await release.Task;
                return true;
            },
            scope);

        var request = interaction.RequestAsync(new ConfirmRequest("Delete?"), cancellation.Token).AsTask();
        await started.Task;

        await cancellation.CancelAsync();
        release.SetResult();
        var result = await request;

        Assert.Equal(InteractionResultStatus.Canceled, result.Status);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task RequestContextCarriesDiagnosticsForPresentationHandlers()
    {
        using var scope = new ActivationScope();
        var interaction = new Interaction<ConfirmRequest, bool>();
        var handler = new RecordingConfirmHandler();

        interaction.RegisterHandler(handler.HandleAsync, scope);

        var result = await interaction.RequestAsync(new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.Completed, result.Status);
        Assert.NotNull(handler.Context);
        Assert.NotEqual(Guid.Empty, handler.Context.RequestId);
        Assert.Equal(typeof(ConfirmRequest), handler.Context.RequestType);
        Assert.Equal(scope.Id, handler.Context.ActivationScopeId);
        Assert.Equal(typeof(RecordingConfirmHandler), handler.Context.HandlerType);
    }

    [Fact]
    public async Task HandlerIsRemovedWhenActivationScopeIsDisposed()
    {
        var scope = new ActivationScope();
        var interaction = new Interaction<ConfirmRequest, bool>();
        interaction.RegisterHandler(
            (_, _) => ValueTask.FromResult(true),
            scope);

        scope.Dispose();

        var result = await interaction.RequestAsync(new ConfirmRequest("Delete?"));

        Assert.Equal(InteractionResultStatus.NotHandled, result.Status);
    }

    [Fact]
    public async Task PendingInteractionIsCanceledWhenActivationScopeIsDisposed()
    {
        var scope = new ActivationScope();
        var interaction = new Interaction<ConfirmRequest, bool>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        interaction.RegisterHandler(
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return true;
            },
            scope);

        var request = interaction.RequestAsync(new ConfirmRequest("Delete?")).AsTask();
        await started.Task;

        scope.Dispose();
        var result = await request;

        Assert.Equal(InteractionResultStatus.Canceled, result.Status);
    }

    private readonly record struct ConfirmRequest(string Message);

    private readonly record struct ConfirmResult(string Message, bool Accepted);

    private sealed class RecordingConfirmHandler
    {
        public InteractionContext<ConfirmRequest>? Context { get; private set; }

        public ValueTask<bool> HandleAsync(
            InteractionContext<ConfirmRequest> context,
            CancellationToken cancellationToken)
        {
            Context = context;
            return ValueTask.FromResult(true);
        }
    }
}
