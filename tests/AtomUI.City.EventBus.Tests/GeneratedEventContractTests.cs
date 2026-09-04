namespace AtomUI.City.EventBus.Tests;

public sealed class GeneratedEventContractTests
{
    [Fact]
    public void PublicAttributesRejectInvalidRuntimeValues()
    {
        Assert.Throws<ArgumentException>(() => new EventContractAttribute(" ", typeof(EventBusModule)));
        Assert.Throws<ArgumentException>(() => new EventContractAttribute("valid", typeof(string)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventContractAttribute("valid", typeof(EventBusModule)) { SchemaVersion = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventChannelAttribute("channel") { Capacity = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventChannelAttribute("channel") { BackpressurePolicy = (EventChannelBackpressurePolicy)99 });
        Assert.Throws<ArgumentException>(() => new EventHandlerAttribute(typeof(EventBusModule)) { ChannelName = " bad" });
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventHandlerAttribute(typeof(EventBusModule)) { ErrorPolicy = (EventErrorPolicy)99 });
    }

    [Fact]
    public void GeneratedSharedDescriptorCarriesVersionAndFingerprint()
    {
        var descriptor = EventContractDescriptor.GeneratedShared<GeneratedEvent>(
            new EventContractId("generated.event"),
            typeof(GeneratedEvent).Assembly,
            3,
            "4AB71C");

        Assert.Equal(3, descriptor.SchemaVersion);
        Assert.Equal("4AB71C", descriptor.SchemaFingerprint);
    }

    private sealed class GeneratedEvent;
}
