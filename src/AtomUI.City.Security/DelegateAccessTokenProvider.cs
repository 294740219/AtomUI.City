namespace AtomUI.City.Security;

public sealed class DelegateAccessTokenProvider : IAccessTokenProvider
{
    private readonly Func<AccessTokenRequest, CancellationToken, ValueTask<AccessTokenResult>> _provider;

    public DelegateAccessTokenProvider(
        Func<AccessTokenRequest, CancellationToken, ValueTask<AccessTokenResult>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
    }

    public async ValueTask<AccessTokenResult> GetTokenAsync(
        AccessTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return AccessTokenResult.Cancelled();
        }

        try
        {
            return await _provider(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return AccessTokenResult.Cancelled();
        }
        catch (Exception exception)
        {
            return AccessTokenResult.Failed(exception.Message, exception);
        }
    }
}
