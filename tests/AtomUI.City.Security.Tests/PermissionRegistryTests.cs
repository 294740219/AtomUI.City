using AtomUI.City.Security;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security.Tests;

public sealed class PermissionRegistryTests
{
    [Fact]
    public void AddStoresPermissionAndIncrementsRevision()
    {
        var registry = new PermissionRegistry();

        var added = registry.Add(new PermissionDescriptor(
            "settings.read",
            displayNameKey: "Permissions.Settings.Read",
            category: "Settings",
            contributionId: "Host"));

        Assert.True(added);
        Assert.Equal(1, registry.Revision);
        Assert.True(registry.TryGet("settings.read", out var descriptor));
        Assert.Equal("Permissions.Settings.Read", descriptor.DisplayNameKey);
        Assert.Equal("Settings", descriptor.Category);
        Assert.Equal("Host", descriptor.ContributionId);
    }

    [Fact]
    public void AddRejectsDuplicatePermissionWithoutChangingRevision()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));

        var added = registry.Add(new PermissionDescriptor("settings.read"));

        Assert.False(added);
        Assert.Equal(1, registry.Revision);
    }

    [Fact]
    public void RemoveByContributionRevokesMatchingPermissions()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("plugin.sales.export", contributionId: "SalesPlugin"));
        registry.Add(new PermissionDescriptor("settings.read", contributionId: "Host"));

        var removed = registry.RemoveByContribution("SalesPlugin");

        Assert.Equal(1, removed);
        Assert.Equal(3, registry.Revision);
        Assert.False(registry.Contains("plugin.sales.export"));
        Assert.True(registry.Contains("settings.read"));
    }

    [Fact]
    public void RemoveByContributionRejectsFutureRegistrationsForRevokedContribution()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("plugin.sales.export", contributionId: "SalesPlugin"));

        var removed = registry.RemoveByContribution("SalesPlugin");
        var addedAfterRevoke = registry.Add(new PermissionDescriptor(
            "plugin.sales.import",
            contributionId: "SalesPlugin"));

        Assert.Equal(1, removed);
        Assert.False(addedAfterRevoke);
        Assert.False(registry.Contains("plugin.sales.import"));
        Assert.Equal(2, registry.Revision);
    }

    [Fact]
    public void RemoveByContributionIsIdempotentAfterContributionWasRevoked()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("plugin.sales.export", contributionId: "SalesPlugin"));
        registry.RemoveByContribution("SalesPlugin");

        var secondRemove = registry.RemoveByContribution("SalesPlugin");

        Assert.Equal(0, secondRemove);
        Assert.Equal(2, registry.Revision);
    }

    [Fact]
    public void PermissionsRejectsExternalListMutation()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));

        var permissions = registry.Permissions;

        var mutable = Assert.IsAssignableFrom<IList<PermissionDescriptor>>(permissions);
        Assert.Throws<NotSupportedException>(() => mutable[0] = new PermissionDescriptor("settings.write"));
    }

    [Fact]
    public async Task ConcurrentChangesPublishInRevisionOrder()
    {
        var registry = new PermissionRegistry();
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var revisions = new List<long>();
        var revisionsSync = new object();
        registry.Changed += (_, args) =>
        {
            if (args.Revision == 1)
            {
                firstEntered.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(5));
            }

            lock (revisionsSync)
            {
                revisions.Add(args.Revision);
            }
        };

        var first = Task.Run(() => registry.Add(new PermissionDescriptor("settings.read")));
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => registry.Add(new PermissionDescriptor("settings.write")));
        await second;
        releaseFirst.Set();
        await first;

        Assert.Equal([1, 2], revisions);
    }

    [Fact]
    public void ObserverFailureDoesNotBreakCommitOrOtherObservers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new PermissionRegistry(diagnostics);
        var secondObserverCalled = false;
        registry.Changed += (_, _) => throw new InvalidOperationException("observer failed");
        registry.Changed += (_, _) => secondObserverCalled = true;

        var added = registry.Add(new PermissionDescriptor("settings.read"));

        Assert.True(added);
        Assert.True(registry.Contains("settings.read"));
        Assert.True(secondObserverCalled);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.PermissionObserverFailed);
    }
}
