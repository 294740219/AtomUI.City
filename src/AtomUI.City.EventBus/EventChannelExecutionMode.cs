namespace AtomUI.City.EventBus;

public enum EventChannelExecutionMode
{
    Serialized = 0,
    Partitioned = 1,
    Concurrent = 2
}
