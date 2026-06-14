using AtomUI.City.Diagnostics;
using AtomUI.City.Threading;

namespace AtomUI.City.Presentation;

public sealed class RouteOutlet : IRouteOutlet
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly SemaphoreSlim _commitGate = new(1, 1);
    private BoundViewHandle? _currentHandle;

    public RouteOutlet(string name, IUiDispatcher dispatcher)
        : this(name, dispatcher, diagnostics: null)
    {
    }

    public RouteOutlet(
        string name,
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Name = name;
        _dispatcher = dispatcher;
        _diagnostics = diagnostics;
    }

    public string Name { get; }

    public object? CurrentContent => _currentHandle?.View;

    public async ValueTask<RouteOutletCommitResult> CommitAsync(
        RouteOutletCommitPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        WriteCommitPlannedDiagnostic(plan);

        if (!string.Equals(plan.OutletName, Name, StringComparison.Ordinal))
        {
            plan.Handle?.Dispose();

            var result = RouteOutletCommitResult.Failed(
                PresentationError.OutletNotFound,
                $"Outlet '{plan.OutletName}' was not found.");
            WriteCommitFailedDiagnostic(plan, result);

            return result;
        }

        var gateEntered = false;

        try
        {
            await _commitGate.WaitAsync(cancellationToken);
            gateEntered = true;

            await _dispatcher.InvokeAsync(
                () => CommitOnUiThread(plan),
                cancellationToken);

            var result = RouteOutletCommitResult.Success();
            WriteCommitSucceededDiagnostic(plan);

            return result;
        }
        catch (Exception exception)
        {
            DisposeRejectedHandle(plan.Handle);

            var result = RouteOutletCommitResult.Failed(
                PresentationError.OutletCommitFailed,
                exception.Message);
            WriteCommitFailedDiagnostic(plan, result);

            return result;
        }
        finally
        {
            if (gateEntered)
            {
                _commitGate.Release();
            }
        }
    }

    private void CommitOnUiThread(RouteOutletCommitPlan plan)
    {
        if (plan.Operation == RouteOutletOperation.Clear)
        {
            var previous = _currentHandle;
            previous?.Dispose();
            _currentHandle = null;
            return;
        }

        if (plan.Handle is null)
        {
            throw new PresentationException(
                PresentationError.OutletCommitFailed,
                "Route outlet replace commit requires a bound view handle.");
        }

        if (ReferenceEquals(_currentHandle, plan.Handle))
        {
            return;
        }

        var old = _currentHandle;
        old?.Dispose();
        _currentHandle = plan.Handle;
    }

    private void DisposeRejectedHandle(BoundViewHandle? handle)
    {
        if (handle is null || ReferenceEquals(handle, _currentHandle))
        {
            return;
        }

        try
        {
            handle.Dispose();
        }
        catch (Exception exception)
        {
            _diagnostics?.Write(new HostDiagnosticRecord(
                PresentationDiagnosticIds.OutletCommitFailed,
                $"Route outlet '{Name}' failed to dispose rejected view handle: {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["outletName"] = Name,
                    ["operation"] = "DisposeRejectedHandle",
                    ["newViewType"] = handle.View.GetType().FullName,
                    ["error"] = exception.GetType().FullName,
                },
            });
        }
    }

    private void WriteCommitPlannedDiagnostic(RouteOutletCommitPlan plan)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.OutletCommitPlanned,
            $"Route outlet '{Name}' received {plan.Operation} commit plan for outlet '{plan.OutletName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(plan, result: null),
        });
    }

    private void WriteCommitSucceededDiagnostic(RouteOutletCommitPlan plan)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.OutletCommitSucceeded,
            $"Route outlet '{Name}' completed {plan.Operation} commit for outlet '{plan.OutletName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(plan, result: null),
        });
    }

    private void WriteCommitFailedDiagnostic(
        RouteOutletCommitPlan plan,
        RouteOutletCommitResult result)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.OutletCommitFailed,
            $"Route outlet '{Name}' failed {plan.Operation} commit for outlet '{plan.OutletName}' with error '{result.Error}': {result.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(plan, result),
        });
    }

    private IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        RouteOutletCommitPlan plan,
        RouteOutletCommitResult? result)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["outletName"] = Name,
            ["requestedOutletName"] = plan.OutletName,
            ["operation"] = plan.Operation.ToString(),
            ["currentViewType"] = _currentHandle?.View.GetType().FullName,
            ["newViewType"] = plan.Handle?.View.GetType().FullName,
            ["error"] = result?.Error?.ToString(),
        };
    }
}
