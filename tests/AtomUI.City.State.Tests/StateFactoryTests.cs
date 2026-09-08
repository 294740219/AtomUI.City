using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.State.Tests;

public sealed class StateFactoryTests
{
    [Fact]
    public void AddStateRegistersSharedRuntimeServices()
    {
        var services = new ServiceCollection();
        services.AddState();
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IStateRegistry>();
        Assert.Same(registry, provider.GetRequiredService<IApplicationState>());
        Assert.Same(registry, provider.GetRequiredService<IApplicationStateWriter>());
        Assert.IsType<StateScopeAccessor>(provider.GetRequiredService<IStateScopeAccessor>());
        Assert.IsType<StateFactory>(provider.GetRequiredService<IStateFactory>());
    }

    [Fact]
    public void ScopeAccessorRestoresNestedScopes()
    {
        var accessor = new StateScopeAccessor();
        using var outer = new StateScope("outer");
        using var inner = new StateScope("inner");

        using (accessor.Push(outer))
        {
            Assert.Same(outer, accessor.Current);

            using (accessor.Push(inner))
            {
                Assert.Same(inner, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void FactoryBindsCreatedStateToCurrentScope()
    {
        var accessor = new StateScopeAccessor();
        var factory = new StateFactory(accessor);
        using var scope = new StateScope("activation");
        WritableState<int> state;

        using (accessor.Push(scope))
        {
            state = factory.CreateWritable(1);
        }

        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => state.SetValue(2));
    }

    [Fact]
    public void FactoryBindsComputedStateToCurrentScope()
    {
        var accessor = new StateScopeAccessor();
        var factory = new StateFactory(accessor);
        using var source = new WritableState<int>(1);
        using var scope = new StateScope("activation");
        ComputedState<int> computed;

        using (accessor.Push(scope))
        {
            computed = factory.CreateComputed(() => source.Value * 2, source);
        }

        Assert.Equal(2, computed.Value);
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => computed.Value);
    }

    [Fact]
    public void FactoryBindsCreatedScopeToCurrentParentScope()
    {
        var accessor = new StateScopeAccessor();
        var factory = new StateFactory(accessor);
        using var parent = new StateScope("parent");
        StateScope child;

        using (accessor.Push(parent))
        {
            child = factory.CreateScope("child");
        }

        Assert.Equal(StateScopeState.Active, child.State);
        parent.Dispose();
        Assert.Equal(StateScopeState.Disposed, child.State);
    }

    [Fact]
    public void FactoryLeavesCreatedScopeOwnedByCallerWithoutCurrentScope()
    {
        var factory = new StateFactory(new StateScopeAccessor());
        using var scope = factory.CreateScope("standalone");

        Assert.Equal(StateScopeState.Active, scope.State);
    }
}
