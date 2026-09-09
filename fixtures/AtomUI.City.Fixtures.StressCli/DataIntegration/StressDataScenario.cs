using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.DataIntegration;

public sealed class StressDataScenario : IAsyncDisposable
{
    private StressDataScenario(
        StressDataServer server,
        IApplicationHost host,
        LifecycleScope projectionOwner)
    {
        Server = server;
        Host = host;
        ProjectionOwner = projectionOwner;
    }

    public StressDataServer Server { get; }

    public IApplicationHost Host { get; }

    public IServiceProvider Services => Host.Services;

    public LifecycleScope ProjectionOwner { get; }

    public static async Task<StressDataScenario> StartAsync(CancellationToken cancellationToken = default)
    {
        var server = await StressDataServer.StartAsync(cancellationToken).ConfigureAwait(false);
        IApplicationHost? host = null;
        try
        {
            host = StressHost.CreateBuilder(server.Endpoints).Build();
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            PhaseD.RegisterCatalog(host.Services.GetRequiredService<IStateRegistry>());
            var root = host.Services.GetRequiredService<LifecycleScope>();
            var projectionOwner = root.CreateChild(LifecycleScopeKind.Subscription, "remote-data-projection");
            host.Services.GetRequiredService<IStressRemoteProjection>().Activate(projectionOwner);
            return new StressDataScenario(server, host, projectionOwner);
        }
        catch
        {
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }

            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        ProjectionOwner.Dispose();
        try
        {
            await Host.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await Host.DisposeAsync().ConfigureAwait(false);
            await Server.DisposeAsync().ConfigureAwait(false);
        }
    }
}
