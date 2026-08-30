using System.Diagnostics;
using System.Globalization;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Presentation;

public sealed class ViewBinder
{
    private readonly IHostDiagnostics? _diagnostics;
    private readonly VisualLifecycleHub? _lifecycleHub;

    public ViewBinder()
    {
    }

    public ViewBinder(IHostDiagnostics diagnostics)
        : this(diagnostics, lifecycleHub: null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
    }

    public ViewBinder(VisualLifecycleHub lifecycleHub)
        : this(diagnostics: null, lifecycleHub)
    {
        ArgumentNullException.ThrowIfNull(lifecycleHub);
    }

    public ViewBinder(
        IHostDiagnostics? diagnostics,
        VisualLifecycleHub? lifecycleHub)
    {
        _diagnostics = diagnostics;
        _lifecycleHub = lifecycleHub;
    }

    public BoundViewHandle Bind(
        ViewDescriptor descriptor,
        object view,
        object viewModel)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);

        var stopwatch = Stopwatch.StartNew();
        IViewDataContextAware? dataContextAware = null;

        try
        {
            if (view is not IViewDataContextAware aware)
            {
                throw new PresentationException(
                    PresentationError.BindingFailed,
                    $"View '{view.GetType().FullName}' does not expose a Presentation data context contract.");
            }

            dataContextAware = aware;
            dataContextAware.DataContext = viewModel;
            _lifecycleHub?.Notify(view, VisualLifecycleEventKind.Attached);

            var handle = BoundViewHandle.Create(
                descriptor,
                view,
                viewModel,
                () =>
                {
                    dataContextAware.DataContext = null;
                    _lifecycleHub?.Notify(view, VisualLifecycleEventKind.Detached);
                });

            stopwatch.Stop();
            WriteBoundDiagnostic(descriptor, view, stopwatch.Elapsed);

            return handle;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            RollbackBinding(view, dataContextAware);
            WriteBindingFailedDiagnostic(descriptor, view, stopwatch.Elapsed, exception);

            throw;
        }
    }

    private static void RollbackBinding(
        object view,
        IViewDataContextAware? dataContextAware)
    {
        if (dataContextAware is not null)
        {
            dataContextAware.DataContext = null;
        }

        if (view is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void WriteBoundDiagnostic(
        ViewDescriptor descriptor,
        object view,
        TimeSpan elapsed)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewBound,
            $"View binder bound view '{view.GetType().FullName}' to view model '{descriptor.ViewModelType.FullName}' in {elapsed.TotalMilliseconds:0.###} ms.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(descriptor, view, elapsed, exception: null),
        });
    }

    private void WriteBindingFailedDiagnostic(
        ViewDescriptor descriptor,
        object view,
        TimeSpan elapsed,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewBindingFailed,
            $"View binder failed to bind view '{view.GetType().FullName}' to view model '{descriptor.ViewModelType.FullName}' in {elapsed.TotalMilliseconds:0.###} ms: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(descriptor, view, elapsed, exception),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        ViewDescriptor descriptor,
        object view,
        TimeSpan elapsed,
        Exception? exception)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["viewModelType"] = descriptor.ViewModelType.FullName,
            ["viewType"] = view.GetType().FullName,
            ["viewKey"] = string.IsNullOrWhiteSpace(descriptor.ViewKey) ? "<default>" : descriptor.ViewKey,
            ["elapsedMilliseconds"] = elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
            ["error"] = exception?.GetType().FullName,
        };
    }
}
