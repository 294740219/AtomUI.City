using System.Runtime.ExceptionServices;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Hosting;

internal sealed class DefaultApplicationHost : IApplicationHost
{
    private readonly IHostDiagnostics _diagnostics;
    private readonly IHost _genericHost;
    private readonly LifecyclePipeline _lifecyclePipeline;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ApplicationHostOptions _options;
    private readonly object _stateSync = new();
    private IServiceScope? _applicationServiceScope;
    private bool _cleanupCompleted;
    private Task? _disposeTask;
    private bool _genericHostStarted;
    private Task? _startTask;
    private CancellationTokenSource? _startupCancellation;
    private ApplicationHostState _state = ApplicationHostState.Created;
    private Task? _stopTask;

    public DefaultApplicationHost(
        IHost genericHost,
        ApplicationContext context,
        IHostDiagnostics diagnostics,
        LifecycleScope hostScope,
        IModuleRegistry moduleRegistry,
        LifecyclePipeline lifecyclePipeline,
        ApplicationHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(genericHost);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(hostScope);
        ArgumentNullException.ThrowIfNull(moduleRegistry);
        ArgumentNullException.ThrowIfNull(lifecyclePipeline);
        ArgumentNullException.ThrowIfNull(options);

        _genericHost = genericHost;
        _diagnostics = diagnostics;
        _moduleRegistry = moduleRegistry;
        _lifecyclePipeline = lifecyclePipeline;
        _options = options;
        Context = context;
        HostScope = hostScope;
    }

    public IServiceProvider Services => _genericHost.Services;

    public IApplicationContext Context { get; }

    public LifecycleScope HostScope { get; }

    public LifecycleScope? ApplicationScope { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Task startTask;
        var ownsStart = false;

        lock (_stateSync)
        {
            ThrowIfDisposed();

            switch (_state)
            {
                case ApplicationHostState.Running:
                    return Task.CompletedTask;
                case ApplicationHostState.Starting:
                    startTask = _startTask!;
                    break;
                case ApplicationHostState.Created:
                    _state = ApplicationHostState.Starting;
                    _startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _startTask = StartCoreAsync(_startupCancellation.Token);
                    startTask = _startTask;
                    ownsStart = true;
                    break;
                case ApplicationHostState.Stopping:
                case ApplicationHostState.Stopped:
                    throw new InvalidOperationException("Application host cannot be started after it has begun stopping.");
                case ApplicationHostState.Faulted:
                    throw new InvalidOperationException("A faulted application host must be stopped or disposed.");
                default:
                    throw new InvalidOperationException($"Application host cannot start from state '{_state}'.");
            }
        }

        return ownsStart || !cancellationToken.CanBeCanceled
            ? startTask
            : startTask.WaitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stopTask;

        lock (_stateSync)
        {
            ThrowIfDisposed();

            switch (_state)
            {
                case ApplicationHostState.Created:
                    _state = ApplicationHostState.Stopped;
                    _stopTask = Task.CompletedTask;
                    stopTask = _stopTask;
                    break;
                case ApplicationHostState.Stopped:
                    stopTask = _stopTask ?? Task.CompletedTask;
                    break;
                case ApplicationHostState.Stopping:
                    stopTask = _stopTask!;
                    break;
                case ApplicationHostState.Starting:
                    _state = ApplicationHostState.Stopping;
                    _startupCancellation?.Cancel();
                    _stopTask = StopAfterStartAsync(_startTask!);
                    stopTask = _stopTask;
                    break;
                case ApplicationHostState.Running:
                case ApplicationHostState.Faulted:
                    _state = ApplicationHostState.Stopping;
                    _stopTask = StopCoreAsync();
                    stopTask = _stopTask;
                    break;
                default:
                    throw new InvalidOperationException($"Application host cannot stop from state '{_state}'.");
            }
        }

        return cancellationToken.CanBeCanceled
            ? stopTask.WaitAsync(cancellationToken)
            : stopTask;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);

        Exception? failure = null;

        try
        {
            await WaitForShutdownSignalAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception stopFailure)
        {
            failure = failure is null
                ? stopFailure
                : new AggregateException(failure, stopFailure);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task WaitForShutdownSignalAsync(CancellationToken cancellationToken)
    {
        var applicationLifetime = Services.GetRequiredService<IHostApplicationLifetime>();

        if (applicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var stoppingRegistration = applicationLifetime.ApplicationStopping.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion);
        using var cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                var (source, token) = ((TaskCompletionSource, CancellationToken))state!;
                source.TrySetCanceled(token);
            },
            (completion, cancellationToken));

        await completion.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        Task.Run(async () => await DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_stateSync)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_state == ApplicationHostState.Disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposeTask = DisposeCoreAsync();

            return new ValueTask(_disposeTask);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteRequiredStageAsync(
                LifecycleStages.ApplicationStart,
                StartTransactionAsync,
                cancellationToken).ConfigureAwait(false);

            lock (_stateSync)
            {
                if (_state == ApplicationHostState.Starting)
                {
                    _state = ApplicationHostState.Running;
                }
            }

            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStarted,
                "Application host has started.",
                HostDiagnosticSeverity.Info));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RollbackStartupAsync().ConfigureAwait(false);
            TransitionStartupFailure();
            throw;
        }
        catch (Exception exception)
        {
            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStartFailed,
                "Application host failed to start.",
                HostDiagnosticSeverity.Error)
            {
                Context = CreateExceptionContext(exception),
            });

            await RollbackStartupAsync().ConfigureAwait(false);
            TransitionStartupFailure();
            throw;
        }
    }

    private async ValueTask StartTransactionAsync(LifecycleContext context)
    {
        await _genericHost.StartAsync(context.CancellationToken).ConfigureAwait(false);
        _genericHostStarted = true;

        _applicationServiceScope = Services.CreateScope();
        ApplicationScope = HostScope.CreateChild(LifecycleScopeKind.Application, "application");
        var applicationServices = _applicationServiceScope.ServiceProvider;

        await _moduleRegistry.ConfigureContributionsAsync(
            (ApplicationContext)Context,
            applicationServices,
            context.CancellationToken).ConfigureAwait(false);

        await ExecuteRequiredStageAsync(
            LifecycleStages.ModuleInitialize,
            async stageContext =>
            {
                await _moduleRegistry.InitializeAsync(
                    (ApplicationContext)Context,
                    applicationServices,
                    stageContext.CancellationToken).ConfigureAwait(false);
            },
            context.CancellationToken).ConfigureAwait(false);

        await ExecuteRequiredStageAsync(
            LifecycleStages.ModuleStart,
            static _ => ValueTask.CompletedTask,
            context.CancellationToken).ConfigureAwait(false);
    }

    private async Task StopAfterStartAsync(Task startTask)
    {
        try
        {
            await startTask.ConfigureAwait(false);
        }
        catch
        {
            // Startup reports its own failure and performs rollback.
        }

        await StopCoreAsync().ConfigureAwait(false);
    }

    private async Task StopCoreAsync()
    {
        var failures = new List<Exception>();

        if (!_cleanupCompleted)
        {
            using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
            await StopTransactionAsync(timeout.Token, failures, isRollback: false).ConfigureAwait(false);

            if (timeout.IsCancellationRequested &&
                !failures.Any(failure => failure is TimeoutException))
            {
                failures.Add(new TimeoutException(
                    $"Application host shutdown exceeded {_options.ShutdownTimeout}."));
            }
        }

        lock (_stateSync)
        {
            _state = ApplicationHostState.Stopped;
        }

        WriteDiagnostic(new HostDiagnosticRecord(
            HostDiagnosticIds.HostStopped,
            "Application host has stopped.",
            HostDiagnosticSeverity.Info));

        if (failures.Count > 0)
        {
            var aggregate = new AggregateException(
                "One or more application host shutdown stages failed.",
                failures).Flatten();

            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStopFailed,
                "Application host stopped with cleanup failures.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["failureCount"] = aggregate.InnerExceptions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["exceptionType"] = aggregate.GetType().FullName,
                },
            });

            throw aggregate;
        }
    }

    private async Task RollbackStartupAsync()
    {
        if (_cleanupCompleted)
        {
            return;
        }

        var failures = new List<Exception>();
        using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
        await StopTransactionAsync(timeout.Token, failures, isRollback: true).ConfigureAwait(false);

        foreach (var failure in failures)
        {
            WriteDiagnostic(new HostDiagnosticRecord(
                HostDiagnosticIds.HostStopFailed,
                "Application host startup rollback failed.",
                HostDiagnosticSeverity.Error)
            {
                Context = CreateExceptionContext(failure, "startupRollback"),
            });
        }
    }

    private async Task StopTransactionAsync(
        CancellationToken cancellationToken,
        ICollection<Exception> failures,
        bool isRollback)
    {
        var context = new LifecycleContext(
            LifecycleStages.ApplicationStop,
            _applicationServiceScope?.ServiceProvider ?? Services,
            cancellationToken);

        try
        {
            await _lifecyclePipeline.ExecuteAsync(
                context,
                async _ =>
                {
                    await ExecuteStopTerminalAsync(cancellationToken, failures).ConfigureAwait(false);
                },
                guaranteeTerminal: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failures.Add(new TimeoutException(
                isRollback
                    ? "Application host startup rollback timed out."
                    : "Application host shutdown timed out."));
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }
        finally
        {
            _cleanupCompleted = true;
        }
    }

    private async Task ExecuteStopTerminalAsync(
        CancellationToken cancellationToken,
        ICollection<Exception> failures)
    {
        try
        {
            await HostScope.StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        var moduleStopContext = new LifecycleContext(
            LifecycleStages.ModuleStop,
            _applicationServiceScope?.ServiceProvider ?? Services,
            cancellationToken);

        try
        {
            await _lifecyclePipeline.ExecuteAsync(
                moduleStopContext,
                async _ =>
                {
                    await _moduleRegistry.ShutdownAsync(
                        (ApplicationContext)Context,
                        _applicationServiceScope?.ServiceProvider ?? Services,
                        cancellationToken).ConfigureAwait(false);
                },
                guaranteeTerminal: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            failures.Add(new TimeoutException("Module shutdown timed out."));
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (_applicationServiceScope is not null)
        {
            try
            {
                if (_applicationServiceScope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _applicationServiceScope.Dispose();
                }
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _applicationServiceScope = null;
            }
        }

        if (_genericHostStarted)
        {
            try
            {
                await _genericHost.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                failures.Add(new TimeoutException("Generic host shutdown timed out."));
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _genericHostStarted = false;
            }
        }
    }

    private async ValueTask ExecuteRequiredStageAsync(
        LifecycleStage stage,
        Func<LifecycleContext, ValueTask> terminal,
        CancellationToken cancellationToken)
    {
        var terminalInvoked = false;
        var context = new LifecycleContext(
            stage,
            _applicationServiceScope?.ServiceProvider ?? Services,
            cancellationToken);

        await _lifecyclePipeline.ExecuteAsync(
            context,
            async stageContext =>
            {
                terminalInvoked = true;
                await terminal(stageContext).ConfigureAwait(false);
            },
            guaranteeTerminal: false).ConfigureAwait(false);

        if (!terminalInvoked)
        {
            throw new InvalidOperationException(
                $"Lifecycle stage '{stage}' was short-circuited before its required terminal operation.");
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        if (_moduleRegistry is IAsyncDisposable asyncModuleRegistry)
        {
            try
            {
                await asyncModuleRegistry.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        if (_applicationServiceScope is not null)
        {
            try
            {
                _applicationServiceScope.Dispose();
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
            finally
            {
                _applicationServiceScope = null;
            }
        }

        try
        {
            await HostScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        try
        {
            if (_genericHost is IAsyncDisposable asyncGenericHost)
            {
                await asyncGenericHost.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                _genericHost.Dispose();
            }
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        _startupCancellation?.Dispose();

        lock (_stateSync)
        {
            _state = ApplicationHostState.Disposed;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more application host disposal stages failed.",
                failures).Flatten();
        }
    }

    private void TransitionStartupFailure()
    {
        lock (_stateSync)
        {
            if (_state == ApplicationHostState.Starting)
            {
                _state = ApplicationHostState.Faulted;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_state == ApplicationHostState.Disposed)
        {
            throw new ObjectDisposedException(nameof(DefaultApplicationHost));
        }
    }

    private void WriteDiagnostic(HostDiagnosticRecord record)
    {
        try
        {
            _diagnostics.Write(record);
        }
        catch
        {
            // Diagnostics must not prevent lifecycle cleanup.
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateExceptionContext(
        Exception exception,
        string? operation = null)
    {
        return new Dictionary<string, string?>
        {
            ["exceptionType"] = exception.GetType().FullName,
            ["operation"] = operation,
        };
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                failures.Add(innerException);
            }

            return;
        }

        failures.Add(exception);
    }

    private enum ApplicationHostState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted,
        Disposed,
    }
}
