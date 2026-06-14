# EventBus Diagnostics Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the `AUC-EVENTBUS-005 Diagnostics` product contract by proving every current EventBus diagnostic code and required context field with focused tests.

**Architecture:** Keep the current in-memory EventBus implementation. Add only diagnostic message context that is already required by the module docs: contract id, event id, subscription id, event type, handler type, and stable subscription lifecycle context. Each behavior is introduced with a failing xUnit test, then the smallest implementation change, then a focused verification and commit.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `AtomUI.City.Diagnostics`, `AtomUI.City.EventBus`, `engineering/check-docs.sh`, `engineering/check-public-api.sh`, `git diff --check`.

---

## File Structure

- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
  - Add focused product-contract tests for every current EventBus diagnostic code.
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
  - Enrich existing diagnostic messages and retain handler type information.
- Modify: `docs/modules/eventbus/features.md`
  - Mark `AUC-EVENTBUS-005` complete after all diagnostics tests are proven.
- Modify: `docs/modules/eventbus/implementation-plan.md`
  - Close the diagnostics implementation gap.
- Modify: `docs/modules/eventbus/testing.md`
  - Mark the diagnostics testing matrix as verified.
- Modify: `docs/modules/eventbus/diagnostics.md`
  - Record the product-level context guarantee.
- Modify: this plan file.

---

### Task 1: EventPublished Diagnostic Includes Event Id

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

Add:

```csharp
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
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventPublishedDiagnosticIncludesStableEventContext"`

Expected: FAIL because `EventPublished` currently omits the event id.

- [x] **Step 3: Write minimal implementation**

Change the `EventPublished` message in `PublishCoreAsync` to include `eventId:D`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventPublishedDiagnosticIncludesStableEventContext"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): include event id in publish diagnostics`

### Task 2: EventAccepted Diagnostic Includes Event Id

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventAcceptedDiagnosticIncludesStableEventContext"`

Expected: FAIL because `EventAccepted` currently omits the event id.

- [ ] **Step 3: Write minimal implementation**

Change the `EventAccepted` message in `PostAsync` to include `eventId:D`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventAcceptedDiagnosticIncludesStableEventContext"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include event id in accepted diagnostics`

### Task 3: EventRejected Diagnostic Includes Event Id

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventRejectedDiagnosticIncludesStableEventContext"`

Expected: FAIL because `EventRejected` currently omits the event id.

- [ ] **Step 3: Write minimal implementation**

Change the `EventRejected` message in `PostAsync` to include `eventId:D`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventRejectedDiagnosticIncludesStableEventContext"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include event id in rejected diagnostics`

### Task 4: EventSubscriptionAdded Diagnostic Includes Event Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public void EventSubscriptionAddedDiagnosticIncludesStableSubscriptionContext()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

    var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventSubscriptionAdded);
    Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
    Assert.Contains(typeof(TestEvent).FullName!, record.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventSubscriptionAddedDiagnosticIncludesStableSubscriptionContext"`

Expected: FAIL because `EventSubscriptionAdded` currently omits event type.

- [ ] **Step 3: Write minimal implementation**

Change the subscription-added diagnostic message to include `subscription.EventType.FullName`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventSubscriptionAddedDiagnosticIncludesStableSubscriptionContext"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): enrich subscription added diagnostics`

### Task 5: EventSubscriptionDisposed Diagnostic Includes Event Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public async Task EventSubscriptionDisposedDiagnosticIncludesStableSubscriptionContext()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
    var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

    await subscription.StopAsync();

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventSubscriptionDisposed);
    Assert.Contains(subscription.Id.ToString(), record.Message, StringComparison.Ordinal);
    Assert.Contains(typeof(TestEvent).FullName!, record.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventSubscriptionDisposedDiagnosticIncludesStableSubscriptionContext"`

Expected: FAIL because `EventSubscriptionDisposed` currently omits event type.

- [ ] **Step 3: Write minimal implementation**

Change the subscription-disposed diagnostic message to include `subscription.EventType.FullName`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventSubscriptionDisposedDiagnosticIncludesStableSubscriptionContext"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): enrich subscription disposed diagnostics`

### Task 6: Failure Diagnostic Includes Event Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public async Task HandlerFailureDiagnosticIncludesEventType()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var contracts = new InMemoryEventContractRegistry();
    contracts.Register(EventContractDescriptor.Shared<TestEvent>(
        new EventContractId("atomui.city.tests.diagnostics.failure.v1"),
        typeof(TestEvent).Assembly));
    var eventBus = new InMemoryEventBus(contracts, diagnostics);

    eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));

    await eventBus.PublishAsync(new TestEvent("failure"));

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    Assert.Contains(typeof(TestEvent).FullName!, record.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerFailureDiagnosticIncludesEventType"`

Expected: FAIL because the failure diagnostic currently omits event type when the contract id is custom.

- [ ] **Step 3: Write minimal implementation**

Change the failure diagnostic message to include `descriptor.EventType.FullName`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerFailureDiagnosticIncludesEventType"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include event type in failure diagnostics`

### Task 7: Failure Diagnostic Includes Handler Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public async Task HandlerFailureDiagnosticIncludesHandlerType()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

    eventBus.Subscribe<TestEvent>(new ThrowingEventHandler());

    await eventBus.PublishAsync(new TestEvent("failure"));

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventDeliveryFailed);
    Assert.Contains(typeof(ThrowingEventHandler).FullName!, record.Message, StringComparison.Ordinal);
}
```

Also add a private `ThrowingEventHandler : IEventHandler<TestEvent>` test helper that throws `InvalidOperationException`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerFailureDiagnosticIncludesHandlerType"`

Expected: FAIL because the concrete `IEventHandler<TEvent>` type is not retained in diagnostics.

- [ ] **Step 3: Write minimal implementation**

Pass the concrete handler type into `SubscribeCore`, store it on `EventSubscription`, and include `HandlerType.FullName` in the failure diagnostic message.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerFailureDiagnosticIncludesHandlerType"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include handler type in failure diagnostics`

### Task 8: Cancellation Diagnostic Includes Event Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public async Task HandlerCancellationDiagnosticIncludesEventType()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var contracts = new InMemoryEventContractRegistry();
    contracts.Register(EventContractDescriptor.Shared<TestEvent>(
        new EventContractId("atomui.city.tests.diagnostics.cancel.v1"),
        typeof(TestEvent).Assembly));
    var eventBus = new InMemoryEventBus(contracts, diagnostics);
    using var cancellation = new CancellationTokenSource();

    eventBus.Subscribe<TestEvent>(context =>
    {
        cancellation.Cancel();
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    });

    await eventBus.PublishAsync(
        new TestEvent("cancel"),
        cancellationToken: cancellation.Token);

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
    Assert.Contains(typeof(TestEvent).FullName!, record.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerCancellationDiagnosticIncludesEventType"`

Expected: FAIL because the cancellation diagnostic currently omits event type when the contract id is custom.

- [ ] **Step 3: Write minimal implementation**

Change the cancellation diagnostic message to include `descriptor.EventType.FullName`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerCancellationDiagnosticIncludesEventType"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include event type in cancellation diagnostics`

### Task 9: Cancellation Diagnostic Includes Handler Type

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

Add:

```csharp
[Fact]
public async Task HandlerCancellationDiagnosticIncludesHandlerType()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
    using var cancellation = new CancellationTokenSource();

    eventBus.Subscribe<TestEvent>(new CancellingEventHandler(cancellation));

    await eventBus.PublishAsync(
        new TestEvent("cancel"),
        cancellationToken: cancellation.Token);

    var record = Assert.Single(
        diagnostics.Records,
        record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
    Assert.Contains(typeof(CancellingEventHandler).FullName!, record.Message, StringComparison.Ordinal);
}
```

Also add a private `CancellingEventHandler : IEventHandler<TestEvent>` test helper that cancels the shared token and observes the context token.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerCancellationDiagnosticIncludesHandlerType"`

Expected: FAIL until cancellation diagnostics include the retained handler type.

- [ ] **Step 3: Write minimal implementation**

Include `HandlerType.FullName` in the cancellation diagnostic message.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~HandlerCancellationDiagnosticIncludesHandlerType"`

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): include handler type in cancellation diagnostics`

### Task 10: Sync EventBus Diagnostics Status

**Files:**
- Modify: `docs/modules/eventbus/features.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/testing.md`
- Modify: `docs/modules/eventbus/diagnostics.md`
- Modify: this plan file

- [ ] **Step 1: Update docs**

Mark `AUC-EVENTBUS-005` as implemented and product-contract tested. Keep other EventBus features at their current status.

- [ ] **Step 2: Run final EventBus and repository gates**

Run:

```bash
dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [ ] **Step 3: Commit**

Commit message: `docs(EventBus): mark diagnostics contract verified`

## Final Verification

After Task 10, run:

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
git status --short --branch
```

Expected: build succeeds, all tests pass, docs/public API/diff checks pass, and worktree is clean.
