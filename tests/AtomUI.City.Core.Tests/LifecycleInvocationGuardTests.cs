using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Tests;

public sealed class LifecycleInvocationGuardTests
{
    [Fact]
    public async Task InvocationFramesFlowAcrossAwaitAndRetainParentOwners()
    {
        var hostOwner = new object();
        var scopeOwner = new object();

        using (LifecycleInvocationGuard.Enter(hostOwner, LifecycleOperationKind.Stop))
        {
            await Task.Yield();

            using (LifecycleInvocationGuard.Enter(scopeOwner, LifecycleOperationKind.Stop))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    LifecycleInvocationGuard.ThrowIfReentrant(
                        hostOwner,
                        LifecycleOperationKind.Dispose));
                Assert.Throws<InvalidOperationException>(() =>
                    LifecycleInvocationGuard.ThrowIfReentrant(
                        scopeOwner,
                        LifecycleOperationKind.Stop));
            }
        }

        LifecycleInvocationGuard.ThrowIfReentrant(hostOwner, LifecycleOperationKind.Stop);
        LifecycleInvocationGuard.ThrowIfReentrant(scopeOwner, LifecycleOperationKind.Stop);
    }
}
