using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus.HeadlessApp;

[ApplicationModule]
[DependsOn(typeof(EventBusModule))]
public sealed class HeadlessApplicationModule : ModuleBase;

[EventContract("atomui.city.eventbus.aot-message.v1", typeof(HeadlessApplicationModule), SchemaVersion = 1)]
[EventChannel("aot", Capacity = 16, BackpressurePolicy = EventChannelBackpressurePolicy.Wait)]
public sealed record AotMessage(int Value);

[EventHandler(typeof(HeadlessApplicationModule), ChannelName = "aot")]
public sealed class AotMessageHandler : IEventHandler<AotMessage>
{
    private static int _lastValue;

    public static int LastValue => Volatile.Read(ref _lastValue);

    public ValueTask HandleAsync(EventContext<AotMessage> context)
    {
        Volatile.Write(ref _lastValue, context.Event.Value);
        return ValueTask.CompletedTask;
    }
}

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args is ["--test-entry-failure"])
        {
            throw new InvalidOperationException("EventBus fixture entry failure.");
        }

        var builder = ApplicationHost.CreateBuilder(args);
        builder.UseModule<EventBusModule>();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.EventBus.HeadlessApp";
            options.ApplicationName = "AtomUI.City.EventBus.HeadlessApp";
        });

        await using var host = builder.Build();
        await host.StartAsync();
        var result = await host.Services.GetRequiredService<IEventPublisher>()
            .PublishAsync(new EventChannel<AotMessage>("aot"), new AotMessage(808));
        var success = result.Deliveries.Count == 1 &&
                      result.Deliveries[0].Succeeded &&
                      AotMessageHandler.LastValue == 808;
        await host.StopAsync();
        Console.WriteLine(success ? "EVENTBUS_AOT_OK" : "EVENTBUS_AOT_FAILED");
        return success ? 0 : 1;
    }
}
