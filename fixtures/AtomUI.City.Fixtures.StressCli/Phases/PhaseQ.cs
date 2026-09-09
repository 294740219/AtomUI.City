using AtomUI.City.Data;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.DataIntegration.Grpc;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.State;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

public static class PhaseQ
{
    private const string GrpcServiceName = "stressdata.StressDataProbe";

    private static readonly Method<InventoryRequest, InventoryReply> InventoryMethod = CreateMethod(
        MethodType.Unary,
        "Inventory",
        InventoryRequest.Parser,
        InventoryReply.Parser);

    private static readonly Method<PriceStreamRequest, PriceUpdate> PriceStreamMethod = CreateMethod(
        MethodType.ServerStreaming,
        "PriceStream",
        PriceStreamRequest.Parser,
        PriceUpdate.Parser);

    private static readonly Method<InventoryDelta, InventoryReply> InventoryDuplexMethod = CreateMethod(
        MethodType.DuplexStreaming,
        "InventoryDuplex",
        InventoryDelta.Parser,
        InventoryReply.Parser);

    public static async Task RunAsync(StressExecutionOptions options, CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var scenario = await StressDataScenario.StartAsync(cancellationToken).ConfigureAwait(false);
        var services = scenario.Services;
        var manager = services.GetRequiredService<DataConnectionManager>();
        var factory = services.GetRequiredService<IStressDataConnectionFactory>();
        var diagnostics = services.GetRequiredService<IDataDiagnostics>();
        var eventBus = services.GetRequiredService<IEventBus>();
        var state = services.GetRequiredService<IApplicationState>();
        var tokenSession = services.GetRequiredService<IStressAccessTokenSession>();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Application, "stress-realtime");
        var grpc = factory.CreateGrpc(scenario.Server.Endpoints, owner);
        var signalR = factory.CreateSignalR(scenario.Server.Endpoints, owner);

        var grpcRegistration = manager.Register(grpc);
        var signalRegistration = manager.Register(signalR);
        Record(
            "Q01-register",
            "gRPC 与 SignalR 连接以 Application owner 注册",
            grpcRegistration.Succeeded && signalRegistration.Succeeded);

        await manager.StartOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
        Record(
            "Q02-start",
            "DataConnectionManager 按 owner 启动两种真实连接",
            grpc.State == DataConnectionState.Connected && signalR.State == DataConnectionState.Connected,
            $"grpc={grpc.State} signalR={signalR.State}");

        var grpcClient = new NativeGrpcClient(grpc, diagnostics);
        var unary = await grpcClient.UnaryAsync(
            InventoryMethod,
            new InventoryRequest { Sku = "SKU-0001" },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Record(
            "Q03-unary",
            "NativeGrpcClient unary 返回真实库存",
            unary.Succeeded && unary.Value!.Quantity == scenario.Server.Backend.Quantity,
            unary.Status.ToString());

        var streamCount = Math.Min(options.DataIterations, 5_000);
        await using (var stream = grpcClient.ServerStreaming(
            PriceStreamMethod,
            new PriceStreamRequest { Sku = "SKU-0001", Count = streamCount },
            new GrpcCallOptions
            {
                Stream = new DataStreamOptions
                {
                    Capacity = 8,
                    BackpressurePolicy = DataBackpressurePolicy.BlockProducer,
                },
            },
            cancellationToken: cancellationToken))
        {
            var received = 0;
            await foreach (var result in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"gRPC price stream failed: {result.Error?.Message}");
                }

                received++;
                await eventBus.PublishAsync(
                    new RemotePriceChanged(
                        result.Value!.Sku,
                        (decimal)result.Value.Price,
                        result.Value.Sequence,
                        tokenSession.Current.Revision),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            Record(
                "Q04-server-stream",
                "有界 BlockProducer gRPC server stream 无丢失并投影 State",
                received == streamCount
                    && state.Get(PhaseD.StateCatalog.RemoteRealtimeUpdates).Value == streamCount,
                $"received={received} projected={state.Get(PhaseD.StateCatalog.RemoteRealtimeUpdates).Value}");
        }

        await using (var duplex = grpcClient.DuplexStreaming(
            InventoryDuplexMethod,
            new GrpcCallOptions
            {
                Stream = new DataStreamOptions
                {
                    Capacity = 4,
                    BackpressurePolicy = DataBackpressurePolicy.Buffer,
                },
            },
            cancellationToken: cancellationToken))
        {
            var responses = new List<InventoryReply>();
            var receiveTask = Task.Run(async () =>
            {
                await foreach (var result in duplex.Responses.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (result.Succeeded)
                    {
                        responses.Add(result.Value!);
                    }
                }
            }, cancellationToken);

            for (var index = 1; index <= 3; index++)
            {
                await duplex.WriteAsync(
                    new InventoryDelta { Sku = "SKU-0001", Delta = index, Sequence = index },
                    cancellationToken).ConfigureAwait(false);
            }

            await duplex.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
            await receiveTask.ConfigureAwait(false);
            Record(
                "Q05-duplex",
                "NativeGrpcClient duplex write/read/complete 全链路完成",
                responses.Count == 3 && responses.Select(reply => reply.Sequence).SequenceEqual([1L, 2L, 3L]),
                $"responses={responses.Count}");
        }

        var inventoryReceived = new TaskCompletionSource<StressInventoryPush>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inventorySubscription = signalR.Subscribe<StressInventoryPush>(
            "InventoryChanged",
            async (message, token) =>
            {
                await eventBus.PublishAsync(
                    new RemoteInventoryChanged(
                        message.Sku,
                        message.Quantity,
                        message.Sequence,
                        tokenSession.Current.Revision),
                    cancellationToken: token).ConfigureAwait(false);
                inventoryReceived.TrySetResult(message);
            });
        var echo = await signalR.InvokeAsync<int>("Echo", [41], cancellationToken).ConfigureAwait(false);
        var push = new StressInventoryPush("SKU-0001", 4321, 10);
        var publish = await signalR.InvokeAsync<long>("PublishInventory", [push], cancellationToken).ConfigureAwait(false);
        var observedPush = await inventoryReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        Record(
            "Q06-signalr",
            "SignalR typed invoke/subscribe 经 EventBus 更新 State",
            echo.Succeeded && echo.Value == 42 && publish.Succeeded && publish.Value == 10
                && observedPush == push
                && state.Get(PhaseD.StateCatalog.RemoteInventory).Value == 4321,
            $"echo={echo.Value} inventory={state.Get(PhaseD.StateCatalog.RemoteInventory).Value}");

        var previous = tokenSession.Current;
        var switched = tokenSession.Switch("user-b");
        await eventBus.PublishAsync(
            new RemotePrincipalSwitched(previous.Revision, switched.Current.Principal, switched.Current.Revision),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await signalR.SwitchPrincipalAsync(switched.Current.Revision, cancellationToken).ConfigureAwait(false);
        await inventorySubscription.RevokeAsync().ConfigureAwait(false);

        await eventBus.PublishAsync(
            new RemoteInventoryChanged("SKU-OLD", 1, 11, previous.Revision),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var beforeCurrentPush = state.Get(PhaseD.StateCatalog.RemoteInventory).Value;

        var currentReceived = new TaskCompletionSource<StressInventoryPush>(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentSubscription = signalR.Subscribe<StressInventoryPush>(
            "InventoryChanged",
            async (message, token) =>
            {
                await eventBus.PublishAsync(
                    new RemoteInventoryChanged(
                        message.Sku,
                        message.Quantity,
                        message.Sequence,
                        switched.Current.Revision),
                    cancellationToken: token).ConfigureAwait(false);
                currentReceived.TrySetResult(message);
            });
        var currentPush = new StressInventoryPush("SKU-NEW", 9876, 12);
        _ = await signalR.InvokeAsync<long>("PublishInventory", [currentPush], cancellationToken).ConfigureAwait(false);
        _ = await currentReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        Record(
            "Q07-principal",
            "账号切换重连、撤销旧订阅并隔离旧 revision 事件",
            beforeCurrentPush == 4321
                && state.Get(PhaseD.StateCatalog.RemotePrincipal).Value == "user-b"
                && state.Get(PhaseD.StateCatalog.RemotePrincipalRevision).Value == switched.Current.Revision
                && state.Get(PhaseD.StateCatalog.RemoteInventory).Value == 9876,
            $"before={beforeCurrentPush} after={state.Get(PhaseD.StateCatalog.RemoteInventory).Value}");

        await currentSubscription.RevokeAsync().ConfigureAwait(false);
        await manager.StopOwnerAsync(owner, cancellationToken).ConfigureAwait(false);
        var afterStop = await signalR.InvokeAsync<int>("Echo", [1], cancellationToken).ConfigureAwait(false);
        Record(
            "Q08-stop",
            "owner 停止后连接收束且拒绝后续调用",
            grpc.State == DataConnectionState.Stopped
                && signalR.State == DataConnectionState.Stopped
                && !afterStop.Succeeded
                && afterStop.Error?.Kind == DataErrorKind.ConnectionClosed,
            $"grpc={grpc.State} signalR={signalR.State} result={afterStop.Status}");
    }

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>(
        MethodType methodType,
        string methodName,
        MessageParser<TRequest> requestParser,
        MessageParser<TResponse> responseParser)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse>
    {
        return new Method<TRequest, TResponse>(
            methodType,
            GrpcServiceName,
            methodName,
            Marshallers.Create(
                static message => message.ToByteArray(),
                payload => requestParser.ParseFrom(payload)),
            Marshallers.Create(
                static message => message.ToByteArray(),
                payload => responseParser.ParseFrom(payload)));
    }

    private static void Record(string id, string description, bool passed, string? detail = null) =>
        FixtureState.Report.Record(id, description, passed, detail);
}
