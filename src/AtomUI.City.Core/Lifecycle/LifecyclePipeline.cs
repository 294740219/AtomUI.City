namespace AtomUI.City.Core.Lifecycle;

public sealed class LifecyclePipeline
{
    private readonly IReadOnlyList<LifecycleMiddleware> _middleware;
    private readonly Func<LifecycleContext, ValueTask> _terminalHandler;

    internal LifecyclePipeline(
        IReadOnlyList<LifecycleMiddleware> middleware,
        Func<LifecycleContext, ValueTask> terminalHandler)
    {
        _middleware = middleware;
        _terminalHandler = terminalHandler;
    }

    public ValueTask ExecuteAsync(LifecycleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteAsync(context, _terminalHandler, guaranteeTerminal: false);
    }

    internal async ValueTask ExecuteAsync(
        LifecycleContext context,
        Func<LifecycleContext, ValueTask> terminalHandler,
        bool guaranteeTerminal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminalHandler);

        var terminalInvoked = false;
        Exception? pipelineFailure = null;

        try
        {
            await InvokeAsync(0).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            pipelineFailure = exception;
        }

        if (guaranteeTerminal && !terminalInvoked)
        {
            try
            {
                await InvokeTerminalAsync(observeCancellation: false).ConfigureAwait(false);
            }
            catch (Exception terminalFailure)
            {
                pipelineFailure = pipelineFailure is null
                    ? terminalFailure
                    : new AggregateException(pipelineFailure, terminalFailure);
            }
        }

        if (pipelineFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(pipelineFailure)
                .Throw();
        }

        return;

        async ValueTask InvokeAsync(int index)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (context.IsShortCircuited)
            {
                return;
            }

            if (index == _middleware.Count)
            {
                await InvokeTerminalAsync(observeCancellation: true).ConfigureAwait(false);
                return;
            }

            var nextInvoked = false;

            async ValueTask Next()
            {
                if (nextInvoked)
                {
                    throw new InvalidOperationException("Lifecycle middleware can invoke next only once.");
                }

                nextInvoked = true;
                await InvokeAsync(index + 1).ConfigureAwait(false);
            }

            await _middleware[index](context, Next).ConfigureAwait(false);
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        async ValueTask InvokeTerminalAsync(bool observeCancellation)
        {
            if (terminalInvoked)
            {
                throw new InvalidOperationException("Lifecycle terminal handler can run only once.");
            }

            if (observeCancellation)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }

            terminalInvoked = true;
            await terminalHandler(context).ConfigureAwait(false);

            if (observeCancellation)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
