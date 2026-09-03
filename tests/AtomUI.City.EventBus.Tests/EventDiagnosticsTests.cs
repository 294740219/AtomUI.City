using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventDiagnosticsTests
{
    [Fact]
    public void EventDiagnosticIdsExposeStableCodeContract()
    {
        var fields = typeof(EventDiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        var codes = fields
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "EventBus.EventAccepted",
                "EventBus.EventDeliveryCancelled",
                "EventBus.EventDeliveryFailed",
                "EventBus.EventPublished",
                "EventBus.EventRejected",
                "EventBus.EventSubscriptionAdded",
                "EventBus.EventSubscriptionDisposed",
                "EventBus.EventSubscriptionQuiescing",
                "EventBus.EventSubscriptionTerminationFailed"
            ],
            codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.StartsWith("EventBus.Event", code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EventPublishedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventPublished);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventAcceptedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PostAsync(new TestEvent("accepted"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventAccepted);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventRejectedDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("rejected"),
            cancellationToken: cancellation.Token);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventRejected);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerFailureIsReportedAndDoesNotStopIndependentHandlers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var independentHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            independentHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.True(independentHandlerCalled);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Deliveries, delivery => !delivery.Succeeded);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerFailureDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var subscription = eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
        Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
        AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
    }

    [Fact]
    public async Task StopPublicationErrorPolicySkipsLaterHandlers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.False(laterHandlerCalled);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task FailPublisherErrorPolicyPropagatesHandlerFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PublishAsync(new TestEvent("failure")));

        Assert.Equal("boom", exception.Message);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerCancellationIsTrackedSeparatelyFromFailure()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();

        eventBus.Subscribe<TestEvent>(context =>
        {
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        var delivery = Assert.Single(result.Deliveries);
        Assert.False(result.Succeeded);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.CanceledCount);
        Assert.False(delivery.Succeeded);
        Assert.True(delivery.Canceled);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
        Assert.DoesNotContain(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    }

    [Fact]
    public async Task HandlerCancellationDiagnosticIncludesStableEventContext()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();

        var subscription = eventBus.Subscribe<TestEvent>(context =>
        {
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
        Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
        Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
        AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
    }

    [Fact]
    public async Task PostedFailPublisherFailureDiagnosticsKeepDeliveryContext()
    {
        var diagnostics = new SignalingHostDiagnostics(EventDiagnosticIds.EventDeliveryFailed, expectedCount: 2);
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var subscription = eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var result = await eventBus.PostAsync(new TestEvent("failure"));
        Assert.True(result.Accepted);

        await diagnostics.WaitForExpectedRecordsAsync();

        var records = diagnostics.Records
            .Where(record => record.Code == EventDiagnosticIds.EventDeliveryFailed)
            .ToArray();

        Assert.Equal(2, records.Length);
        Assert.All(records, record =>
        {
            Assert.Contains(result.ContractId.Value, record.Message, StringComparison.Ordinal);
            Assert.Contains(result.EventId.ToString("D"), record.Message, StringComparison.Ordinal);
            Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
            AssertDeliveryContext(record, result.ContractId, result.EventId, subscription.Id);
        });
    }

    [Fact]
    public async Task HandlerCancellationStopsLaterDeliveriesWithoutThrowing()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(context =>
        {
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        });
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(
            new TestEvent("cancel"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.False(laterHandlerCalled);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.CanceledCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task PostAsyncAcceptedPublicationWritesDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        var result = await eventBus.PostAsync(new TestEvent("accepted"));

        Assert.True(result.Accepted);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventAccepted);
    }

    [Fact]
    public async Task PostAsyncRejectedPublicationWritesDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("rejected"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Accepted);
        Assert.Contains(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventRejected);
    }

    private sealed record TestEvent(string Value);

    private static void AssertDeliveryContext(
        HostDiagnosticRecord record,
        EventContractId contractId,
        Guid eventId,
        EventSubscriptionId subscriptionId)
    {
        AssertContextValue(record, "contractId", contractId.Value);
        AssertContextValue(record, "eventId", eventId.ToString("D"));
        AssertContextValue(record, "subscriptionId", subscriptionId.ToString());
    }

    private static void AssertContextValue(
        HostDiagnosticRecord record,
        string key,
        string expectedValue)
    {
        Assert.True(
            record.Context.TryGetValue(key, out var actualValue),
            $"Diagnostic '{record.Code}' must include context key '{key}'.");
        Assert.Equal(expectedValue, actualValue);
    }

    private sealed class SignalingHostDiagnostics : IHostDiagnostics
    {
        private readonly object _syncRoot = new();
        private readonly List<HostDiagnosticRecord> _records = [];
        private readonly string _targetCode;
        private readonly int _expectedCount;
        private readonly TaskCompletionSource _expectedRecords = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public SignalingHostDiagnostics(string targetCode, int expectedCount)
        {
            _targetCode = targetCode;
            _expectedCount = expectedCount;
        }

        public IReadOnlyList<HostDiagnosticRecord> Records
        {
            get
            {
                lock (_syncRoot)
                {
                    return Array.AsReadOnly(_records.ToArray());
                }
            }
        }

        public void Write(HostDiagnosticRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            lock (_syncRoot)
            {
                _records.Add(record);
                if (_records.Count(item => item.Code == _targetCode) >= _expectedCount)
                {
                    _expectedRecords.TrySetResult();
                }
            }
        }

        public void Complete()
        {
        }

        public async Task WaitForExpectedRecordsAsync()
        {
            await _expectedRecords.Task
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }
    }
}
