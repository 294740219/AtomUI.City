using AtomUI.City.Diagnostics;
using AtomUI.City.Lifecycle;
using AtomUI.City.Modularity;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Hosting;

internal sealed class DefaultApplicationHost : IApplicationHost
{
    private readonly IHost _genericHost;
    private readonly IHostDiagnostics _diagnostics;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private bool _disposed;
    private bool _started;
    private bool _stopped;

    public DefaultApplicationHost(
        IHost genericHost,
        ApplicationContext context,
        IHostDiagnostics diagnostics,
        LifecycleScope hostScope,
        IModuleRegistry moduleRegistry)
    {
        ArgumentNullException.ThrowIfNull(genericHost);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(hostScope);
        ArgumentNullException.ThrowIfNull(moduleRegistry);

        _genericHost = genericHost;
        _diagnostics = diagnostics;
        _moduleRegistry = moduleRegistry;
        Context = context;
        HostScope = hostScope;
    }

    public IServiceProvider Services => _genericHost.Services;

    public IApplicationContext Context { get; }

    public LifecycleScope HostScope { get; }

    public LifecycleScope? ApplicationScope { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_stopped)
            {
                throw new InvalidOperationException("Application host cannot be started after it has stopped.");
            }

            if (_started)
            {
                return;
            }

            var genericHostStarted = false;

            try
            {
                await _genericHost.StartAsync(cancellationToken).ConfigureAwait(false);
                genericHostStarted = true;
                ApplicationScope = HostScope.CreateChild(LifecycleScopeKind.Application, "application");
                await _moduleRegistry.ConfigureContributionsAsync(
                    (ApplicationContext)Context,
                    Services,
                    cancellationToken).ConfigureAwait(false);
                await _moduleRegistry.InitializeAsync(
                    (ApplicationContext)Context,
                    Services,
                    cancellationToken).ConfigureAwait(false);
                _started = true;
                WriteDiagnostic(new HostDiagnosticRecord(
                    HostDiagnosticIds.HostStarted,
                    "Application host has started.",
                    HostDiagnosticSeverity.Info));
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

                if (genericHostStarted)
                {
                    try
                    {
                        await _genericHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception stopException)
                    {
                        WriteDiagnostic(new HostDiagnosticRecord(
                            HostDiagnosticIds.HostStopFailed,
                            "Application host failed to stop after startup failure.",
                            HostDiagnosticSeverity.Error)
                        {
                            Context = CreateExceptionContext(stopException),
                        });
                    }
                }

                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (!_started || _stopped)
            {
                return;
            }

            try
            {
                await _moduleRegistry.ShutdownAsync(
                    (ApplicationContext)Context,
                    Services,
                    cancellationToken).ConfigureAwait(false);

                if (ApplicationScope is not null)
                {
                    await ApplicationScope.StopAsync().ConfigureAwait(false);
                }

                await HostScope.StopAsync().ConfigureAwait(false);
                await _genericHost.StopAsync(cancellationToken).ConfigureAwait(false);
                _started = false;
                _stopped = true;
                WriteDiagnostic(new HostDiagnosticRecord(
                    HostDiagnosticIds.HostStopped,
                    "Application host has stopped.",
                    HostDiagnosticSeverity.Info));
            }
            catch (Exception exception)
            {
                _started = false;
                _stopped = true;
                WriteDiagnostic(new HostDiagnosticRecord(
                    HostDiagnosticIds.HostStopFailed,
                    "Application host failed to stop.",
                    HostDiagnosticSeverity.Error)
                {
                    Context = CreateExceptionContext(exception),
                });

                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        await _genericHost.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        await StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
        HostScope.Dispose();
        _genericHost.Dispose();
        _stateLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        await HostScope.DisposeAsync().ConfigureAwait(false);

        if (_genericHost is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            _stateLock.Dispose();
            return;
        }

        _genericHost.Dispose();
        _stateLock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
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

    private static IReadOnlyDictionary<string, string?> CreateExceptionContext(Exception exception)
    {
        return new Dictionary<string, string?>
        {
            ["exceptionType"] = exception.GetType().FullName,
        };
    }
}
