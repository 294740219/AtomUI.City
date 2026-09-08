using System.Collections.ObjectModel;

namespace AtomUI.City.Routing;

public sealed class RouteResolveResult
{
    private RouteResolveResult(
        RouteResolveResultStatus status,
        IReadOnlyDictionary<string, object?> data,
        NavigationTarget? redirectTarget,
        string? code,
        string? message,
        Exception? exception)
    {
        Status = status;
        Data = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(data, StringComparer.Ordinal));
        RedirectTarget = redirectTarget;
        Code = code;
        Message = message;
        Exception = exception;
    }

    public RouteResolveResultStatus Status { get; }
    public IReadOnlyDictionary<string, object?> Data { get; }
    public NavigationTarget? RedirectTarget { get; }
    public string? Code { get; }
    public string? Message { get; }
    public Exception? Exception { get; }

    public static RouteResolveResult Success(IReadOnlyDictionary<string, object?>? data = null) =>
        new(RouteResolveResultStatus.Success, data ?? EmptyData(), null, null, null, null);

    public static RouteResolveResult NotFound(string? message = null) =>
        new(RouteResolveResultStatus.NotFound, EmptyData(), null, "CITY-NAVIGATION-RESOLVER-NOT-FOUND", message, null);

    public static RouteResolveResult Redirect(NavigationTarget target) =>
        new(RouteResolveResultStatus.Redirect, EmptyData(), target ?? throw new ArgumentNullException(nameof(target)), null, null, null);

    public static RouteResolveResult Cancelled(string? message = null) =>
        new(RouteResolveResultStatus.Cancelled, EmptyData(), null, "CITY-NAVIGATION-CANCELLED", message, null);

    public static RouteResolveResult Failed(string code, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(RouteResolveResultStatus.Failed, EmptyData(), null, code, message, exception);
    }

    private static IReadOnlyDictionary<string, object?> EmptyData() =>
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
