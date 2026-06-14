using System.Diagnostics;
using System.Globalization;
using AtomUI.City.Diagnostics;
using AtomUI.City.Threading;

namespace AtomUI.City.Presentation;

public sealed class ViewFactory
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IServiceProvider _services;
    private readonly IHostDiagnostics? _diagnostics;

    public ViewFactory(IUiDispatcher dispatcher)
        : this(dispatcher, EmptyServiceProvider.Instance, diagnostics: null)
    {
    }

    public ViewFactory(IUiDispatcher dispatcher, IServiceProvider services)
        : this(dispatcher, services, diagnostics: null)
    {
    }

    public ViewFactory(IUiDispatcher dispatcher, IHostDiagnostics? diagnostics)
        : this(dispatcher, EmptyServiceProvider.Instance, diagnostics)
    {
    }

    public ViewFactory(
        IUiDispatcher dispatcher,
        IServiceProvider services,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(services);

        _dispatcher = dispatcher;
        _services = services;
        _diagnostics = diagnostics;
    }

    public async ValueTask<object> CreateAsync(
        ViewDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var view = await _dispatcher.InvokeAsync(
                () => descriptor.CreateView(new ViewFactoryContext(_services)),
                cancellationToken);
            stopwatch.Stop();
            WriteCreatedDiagnostic(descriptor, stopwatch.Elapsed);

            return view;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteCreationFailedDiagnostic(descriptor, stopwatch.Elapsed, exception);

            throw;
        }
    }

    private void WriteCreatedDiagnostic(ViewDescriptor descriptor, TimeSpan elapsed)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewCreated,
            $"View factory created view '{descriptor.ViewType.FullName}' for view model '{descriptor.ViewModelType.FullName}' in {elapsed.TotalMilliseconds:0.###} ms.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(descriptor, elapsed, exception: null),
        });
    }

    private void WriteCreationFailedDiagnostic(
        ViewDescriptor descriptor,
        TimeSpan elapsed,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewCreationFailed,
            $"View factory failed to create view '{descriptor.ViewType.FullName}' for view model '{descriptor.ViewModelType.FullName}' in {elapsed.TotalMilliseconds:0.###} ms: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(descriptor, elapsed, exception),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        ViewDescriptor descriptor,
        TimeSpan elapsed,
        Exception? exception)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["viewModelType"] = descriptor.ViewModelType.FullName,
            ["viewType"] = descriptor.ViewType.FullName,
            ["viewKey"] = string.IsNullOrWhiteSpace(descriptor.ViewKey) ? "<default>" : descriptor.ViewKey,
            ["constructorParameters"] = FormatConstructorParameters(descriptor),
            ["elapsedMilliseconds"] = elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
            ["error"] = exception?.GetType().FullName,
        };
    }

    private static string FormatConstructorParameters(ViewDescriptor descriptor)
    {
        return descriptor.ConstructorParameterTypes.Count == 0
            ? string.Empty
            : string.Join(
                ";",
                descriptor.ConstructorParameterTypes.Select(static type => type.FullName ?? type.Name));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
