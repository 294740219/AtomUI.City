using AtomUI.City.Mvvm;

namespace AtomUI.City.Mvvm.Tests;

public sealed class DeactivationTests
{
    [Fact]
    public async Task DeactivationGuardReturnsAllowWhenViewModelHasNoContract()
    {
        var result = await DeactivationGuard.CanDeactivateAsync(new object(), CancellationToken.None);

        Assert.Equal(DeactivationStatus.Allow, result.Status);
    }

    [Fact]
    public async Task DeactivationGuardReturnsRejectWithoutCallingConfirmWhenCanDeactivateRejects()
    {
        var viewModel = new RejectingDeactivateViewModel();

        var result = await DeactivationGuard.CanDeactivateAsync(viewModel, CancellationToken.None);

        Assert.Equal(DeactivationStatus.Reject, result.Status);
        Assert.Equal("dirty", result.Reason);
        Assert.Equal(1, viewModel.CanDeactivateCount);
        Assert.Equal(0, viewModel.ConfirmDeactivateCount);
    }

    [Fact]
    public async Task DeactivationGuardRunsConfirmAfterCanDeactivateAllows()
    {
        var viewModel = new ConfirmingDeactivateViewModel();

        var result = await DeactivationGuard.CanDeactivateAsync(viewModel, CancellationToken.None);

        Assert.Equal(DeactivationStatus.Cancel, result.Status);
        Assert.Equal("user-cancelled", result.Reason);
        Assert.Equal(["can-deactivate", "confirm-deactivate"], viewModel.Calls);
    }

    [Fact]
    public async Task DeactivationGuardReturnsCancelForCanceledTokenAndSkipsViewModel()
    {
        using var cancellation = new CancellationTokenSource();
        var viewModel = new RejectingDeactivateViewModel();
        await cancellation.CancelAsync();

        var result = await DeactivationGuard.CanDeactivateAsync(viewModel, cancellation.Token);

        Assert.Equal(DeactivationStatus.Cancel, result.Status);
        Assert.Equal("deactivation-cancelled", result.Reason);
        Assert.Equal(0, viewModel.CanDeactivateCount);
        Assert.Equal(0, viewModel.ConfirmDeactivateCount);
    }

    [Fact]
    public async Task DeactivationGuardMapsExceptionsToFailedResult()
    {
        var exception = new InvalidOperationException("cannot leave");
        var viewModel = new ThrowingDeactivateViewModel(exception);

        var result = await DeactivationGuard.CanDeactivateAsync(viewModel, CancellationToken.None);

        Assert.Equal(DeactivationStatus.Failed, result.Status);
        Assert.Equal("deactivation-failed", result.Reason);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task CanDeactivateContractReturnsAllowResult()
    {
        ICanDeactivate viewModel = new AllowDeactivateViewModel();

        var result = await viewModel.CanDeactivateAsync(CancellationToken.None);

        Assert.Equal(DeactivationStatus.Allow, result.Status);
    }

    [Fact]
    public async Task ConfirmDeactivateContractCanReturnCancelResult()
    {
        IConfirmDeactivate viewModel = new CancelDeactivateViewModel();

        var result = await viewModel.ConfirmDeactivateAsync(CancellationToken.None);

        Assert.Equal(DeactivationStatus.Cancel, result.Status);
        Assert.Equal("user-cancelled", result.Reason);
    }

    private sealed class AllowDeactivateViewModel : ICanDeactivate
    {
        public ValueTask<DeactivationResult> CanDeactivateAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(DeactivationResult.Allow());
        }
    }

    private sealed class CancelDeactivateViewModel : IConfirmDeactivate
    {
        public ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(DeactivationResult.Cancel("user-cancelled"));
        }
    }

    private sealed class RejectingDeactivateViewModel : ICanDeactivate, IConfirmDeactivate
    {
        public int CanDeactivateCount { get; private set; }

        public int ConfirmDeactivateCount { get; private set; }

        public ValueTask<DeactivationResult> CanDeactivateAsync(CancellationToken cancellationToken)
        {
            CanDeactivateCount++;
            return ValueTask.FromResult(DeactivationResult.Reject("dirty"));
        }

        public ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken)
        {
            ConfirmDeactivateCount++;
            return ValueTask.FromResult(DeactivationResult.Allow());
        }
    }

    private sealed class ConfirmingDeactivateViewModel : ICanDeactivate, IConfirmDeactivate
    {
        public List<string> Calls { get; } = [];

        public ValueTask<DeactivationResult> CanDeactivateAsync(CancellationToken cancellationToken)
        {
            Calls.Add("can-deactivate");
            return ValueTask.FromResult(DeactivationResult.Allow());
        }

        public ValueTask<DeactivationResult> ConfirmDeactivateAsync(CancellationToken cancellationToken)
        {
            Calls.Add("confirm-deactivate");
            return ValueTask.FromResult(DeactivationResult.Cancel("user-cancelled"));
        }
    }

    private sealed class ThrowingDeactivateViewModel(Exception exception) : ICanDeactivate
    {
        public ValueTask<DeactivationResult> CanDeactivateAsync(CancellationToken cancellationToken)
        {
            throw exception;
        }
    }
}
