namespace AtomUI.City.Mvvm;

public static class DeactivationGuard
{
    public const string CanceledReason = "deactivation-cancelled";

    public const string FailedReason = "deactivation-failed";

    public static async ValueTask<DeactivationResult> CanDeactivateAsync(
        object viewModel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (cancellationToken.IsCancellationRequested)
        {
            return DeactivationResult.Cancel(CanceledReason);
        }

        try
        {
            if (viewModel is ICanDeactivate canDeactivate)
            {
                var result = Normalize(
                    await canDeactivate.CanDeactivateAsync(cancellationToken).ConfigureAwait(false));

                if (result.Status != DeactivationStatus.Allow)
                {
                    return result;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return DeactivationResult.Cancel(CanceledReason);
            }

            if (viewModel is IConfirmDeactivate confirmDeactivate)
            {
                return Normalize(
                    await confirmDeactivate.ConfirmDeactivateAsync(cancellationToken).ConfigureAwait(false));
            }

            return DeactivationResult.Allow();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DeactivationResult.Cancel(CanceledReason);
        }
        catch (Exception exception)
        {
            return DeactivationResult.Failed(FailedReason, exception);
        }
    }

    private static DeactivationResult Normalize(DeactivationResult? result)
    {
        return result ?? DeactivationResult.Failed(
            "deactivation-result-null",
            new InvalidOperationException("Deactivation contract returned a null result."));
    }
}
