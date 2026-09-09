using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Data.Tests;

public sealed class DataHostIntegrationTests
{
    [Fact]
    public async Task DataModuleStopsRegisteredConnectionsWithHost()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Data.Tests";
            options.ApplicationName = "AtomUI.City.Data.Tests";
        });
        builder.UseModule<DataModule>();

        await using var host = builder.Build();
        await host.StartAsync();
        var manager = host.Services.GetRequiredService<DataConnectionManager>();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "test-host");
        var connection = new HostConnection(owner);
        Assert.True(manager.Register(connection).Succeeded);
        await manager.StartOwnerAsync(owner);

        await host.StopAsync();

        var rejectedRequest = await host.Services
            .GetRequiredService<IDataRequestPipeline>()
            .SendAsync(new DataRequest<string>("host", "after-stop", DataTransportKind.Http));

        Assert.Equal(1, connection.StartCount);
        Assert.Equal(1, connection.StopCount);
        Assert.Equal(DataConnectionState.Stopped, connection.State);
        Assert.False(manager.Register(new HostConnection(owner)).Succeeded);
        Assert.Equal(DataErrorKind.PolicyRejected, rejectedRequest.Error?.Kind);
    }

    [Fact]
    public async Task DataModuleCancelsAndDrainsInflightRequestsBeforeHostStops()
    {
        var transport = new BlockingTransport();
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Data.Tests.Drain";
            options.ApplicationName = "AtomUI.City.Data.Tests.Drain";
        });
        builder.UseModule<DataModule>();
        builder.ConfigureServices(services =>
            services.Insert(0, ServiceDescriptor.Singleton<IRequestResponseTransport>(transport)));

        await using var host = builder.Build();
        await host.StartAsync();
        var pipeline = host.Services.GetRequiredService<IDataRequestPipeline>();
        var request = pipeline.SendAsync(new DataRequest<string>(
            "host",
            "inflight",
            DataTransportKind.Http)).AsTask();
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = host.StopAsync();
        await transport.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await request.WaitAsync(TimeSpan.FromSeconds(2));
        await stop.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DataResultStatus.Cancelled, result.Status);
        Assert.True(transport.Exited);
    }

    private sealed class HostConnection : IDataConnection
    {
        public HostConnection(DataConnectionOwner owner)
        {
            Owner = owner;
        }

        public string ConnectionId { get; } = $"host-{Guid.NewGuid():N}";

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            State = DataConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingTransport : IRequestResponseTransport
    {
        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Exited { get; private set; }

        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The blocking transport unexpectedly completed.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
            finally
            {
                Exited = true;
            }
        }
    }
}
