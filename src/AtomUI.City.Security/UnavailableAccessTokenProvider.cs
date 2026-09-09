using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class UnavailableAccessTokenProvider : IAccessTokenProvider
{
    private readonly IHostDiagnostics? _diagnostics;

    public UnavailableAccessTokenProvider()
    {
    }

    public UnavailableAccessTokenProvider(IHostDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = cancellationToken.IsCancellationRequested
                ? AccessTokenResult.Cancelled()
                : AccessTokenResult.Unavailable("No access token provider is configured.");
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.AccessTokenResolved,
            "Access token request completed.",
            HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["resourceName"] = request.ResourceName,
                ["scheme"] = request.Scheme,
                ["operationName"] = request.OperationName,
                ["status"] = result.Status.ToString(),
            });
        return ValueTask.FromResult(result);
    }
}
