using System.Security.Claims;

namespace AtomUI.City.Security;

public interface IAuthorizationEvaluator
{
    ValueTask<AuthorizationResult> EvaluateAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<AuthorizationResult> EvaluatePolicyAsync(
        ClaimsPrincipal? principal,
        string policyName,
        string? resourceName = null,
        string? contributionId = null,
        CancellationToken cancellationToken = default);
}
