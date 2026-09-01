using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Core.Lifecycle;

public sealed class LifecyclePipeline
{
    private readonly IReadOnlyList<LifecycleMiddlewareRegistration> _middleware;
    private readonly Func<LifecycleContext, ValueTask> _terminalHandler;

    internal LifecyclePipeline(
        IReadOnlyList<LifecycleMiddlewareRegistration> middleware,
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
        bool guaranteeTerminal,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminalHandler);

        var terminalInvoked = false;
        Exception? pipelineFailure = null;
        var attributedFailures = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var terminalFailures = new HashSet<Exception>(ReferenceEqualityComparer.Instance);

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

            var registration = _middleware[index];

            if (registration.Stage is { } stage && context.Stage != stage)
            {
                await InvokeAsync(index + 1).ConfigureAwait(false);
                context.CancellationToken.ThrowIfCancellationRequested();
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

            try
            {
                await registration.Handler(context, Next).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (!IsExpectedCancellation(exception) &&
                    !terminalFailures.Contains(exception) &&
                    attributedFailures.Add(exception))
                {
                    WriteMiddlewareFailure(diagnostics, context, registration, exception);
                }

                throw;
            }

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

            try
            {
                await terminalHandler(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                terminalFailures.Add(exception);
                throw;
            }

            if (observeCancellation)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }

        bool IsExpectedCancellation(Exception exception)
        {
            return exception is OperationCanceledException &&
                   context.CancellationToken.IsCancellationRequested;
        }
    }

    private static void WriteMiddlewareFailure(
        IHostDiagnostics? diagnostics,
        LifecycleContext context,
        LifecycleMiddlewareRegistration registration,
        Exception exception)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.LifecycleMiddlewareFailed,
                "Lifecycle middleware execution failed.",
                HostDiagnosticSeverity.Error,
                Stage: context.Stage)
            {
                Context = new Dictionary<string, string?>
                {
                    ["middlewareType"] = registration.MiddlewareType.FullName
                                         ?? registration.MiddlewareType.Name,
                    ["operationId"] = context.OperationId,
                    ["exceptionType"] = exception.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics must not replace the original lifecycle failure.
        }
    }
}
