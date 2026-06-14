# EventBus Context And Options Boundary Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden AtomUI.City.EventBus publish option and event context boundaries so invalid correlation ids, causation ids, event ids, subscription ids, publish depth, and dispatch policies cannot enter public contracts.

**Architecture:** Keep the existing in-memory EventBus and add narrow validation to `EventPublishOptions` init properties and `EventContext<TEvent>` constructor inputs. Tests stay in `tests/AtomUI.City.EventBus.Tests` and each task proves one public boundary with a red/green cycle.

**Tech Stack:** .NET, xUnit, AtomUI.City.EventBus.

---

### Task 1: Reject negative publish depth in options init

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishOptions.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PublishOptionsRejectNegativePublishDepthInit()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new EventPublishOptions
    {
        PublishDepth = -1,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectNegativePublishDepthInit"`
Expected: FAIL because `EventPublishOptions` currently accepts negative depth until publish entry validation.

- [x] **Step 3: Write minimal implementation**

Back `EventPublishOptions.PublishDepth` with an init setter that rejects values below 0. Update `api-contracts.md` so `EventPublishOptions` documents init and publish-entry validation.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectNegativePublishDepthInit"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate publish option depth`

### Task 2: Reject invalid correlation ids in publish options

**Files:**
- Create: `src/AtomUI.City.EventBus/EventCorrelationIds.cs`
- Modify: `src/AtomUI.City.EventBus/EventPublishOptions.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(" trace ")]
[InlineData("trace\nid")]
public void PublishOptionsRejectInvalidCorrelationIds(string correlationId)
{
    Assert.Throws<ArgumentException>(() => new EventPublishOptions
    {
        CorrelationId = correlationId,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectInvalidCorrelationIds"`
Expected: FAIL because correlation ids currently accept blank, surrounding whitespace, and control characters.

- [x] **Step 3: Write minimal implementation**

Add internal `EventCorrelationIds.ValidateOptional` for optional ids and call it from `EventPublishOptions.CorrelationId`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectInvalidCorrelationIds"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate publish correlation ids`

### Task 3: Reject invalid causation ids in publish options

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishOptions.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(" cause ")]
[InlineData("cause\nid")]
public void PublishOptionsRejectInvalidCausationIds(string causationId)
{
    Assert.Throws<ArgumentException>(() => new EventPublishOptions
    {
        CausationId = causationId,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectInvalidCausationIds"`
Expected: FAIL because causation ids currently accept blank, surrounding whitespace, and control characters.

- [x] **Step 3: Write minimal implementation**

Call `EventCorrelationIds.ValidateOptional` from `EventPublishOptions.CausationId`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishOptionsRejectInvalidCausationIds"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate publish causation ids`

### Task 4: Reject default contract ids in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventContextRejectsDefaultContractId()
{
    Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        default,
        Guid.NewGuid(),
        "correlation",
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsDefaultContractId"`
Expected: FAIL because `EventContext<TEvent>` currently accepts default contract ids.

- [x] **Step 3: Write minimal implementation**

Call `EventContractId.ThrowIfDefault(contractId, nameof(contractId))` in the `EventContext<TEvent>` constructor. Add an `EventContext<TEvent> constructor` row to `api-contracts.md`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsDefaultContractId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context contract ids`

### Task 5: Reject empty event ids in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventContextRejectsEmptyEventId()
{
    Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.Empty,
        "correlation",
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsEmptyEventId"`
Expected: FAIL because `EventContext<TEvent>` currently accepts empty event ids.

- [x] **Step 3: Write minimal implementation**

Throw `ArgumentException` when `eventId == Guid.Empty`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsEmptyEventId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context event ids`

### Task 6: Reject invalid correlation ids in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventCorrelationIds.cs`
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData(" trace ")]
[InlineData("trace\nid")]
public void EventContextRejectsInvalidCorrelationIds(string correlationId)
{
    Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.NewGuid(),
        correlationId,
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsInvalidCorrelationIds"`
Expected: FAIL because `EventContext<TEvent>` currently rejects blank correlation ids but accepts surrounding whitespace and control characters.

- [x] **Step 3: Write minimal implementation**

Add `EventCorrelationIds.ValidateRequired` and use it for `EventContext<TEvent>.CorrelationId`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsInvalidCorrelationIds"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context correlation ids`

### Task 7: Reject negative publish depth in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventContextRejectsNegativePublishDepth()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.NewGuid(),
        "correlation",
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: -1,
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsNegativePublishDepth"`
Expected: FAIL because `EventContext<TEvent>` currently accepts negative publish depth.

- [x] **Step 3: Write minimal implementation**

Throw `ArgumentOutOfRangeException` when `publishDepth < 0`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsNegativePublishDepth"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context publish depth`

### Task 8: Reject default subscription ids in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventContextRejectsDefaultSubscriptionId()
{
    Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.NewGuid(),
        "correlation",
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        default,
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsDefaultSubscriptionId"`
Expected: FAIL because `EventContext<TEvent>` currently accepts default subscription ids.

- [x] **Step 3: Write minimal implementation**

Call `EventSubscriptionId.ThrowIfDefault(subscriptionId, nameof(subscriptionId))` in the context constructor.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsDefaultSubscriptionId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context subscription ids`

### Task 9: Reject unknown dispatch policies in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventContextRejectsUnknownDispatchPolicy()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.NewGuid(),
        "correlation",
        causationId: null,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        EventSubscriptionId.New(),
        (EventDispatchPolicy)999,
        CancellationToken.None));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsUnknownDispatchPolicy"`
Expected: FAIL because `EventContext<TEvent>` currently accepts unknown dispatch policies.

- [x] **Step 3: Write minimal implementation**

Validate `Enum.IsDefined(dispatchPolicy)` in the context constructor.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsUnknownDispatchPolicy"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate context dispatch policies`

### Task 10: Reject invalid causation ids in event contexts

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContext.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(" cause ")]
[InlineData("cause\nid")]
public void EventContextRejectsInvalidCausationIds(string causationId)
{
    Assert.Throws<ArgumentException>(() => new EventContext<TestEvent>(
        new TestEvent("context"),
        new EventContractId("atomui.city.tests.context.v1"),
        Guid.NewGuid(),
        "correlation",
        causationId,
        DateTimeOffset.UtcNow,
        publishDepth: 0,
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        CancellationToken.None));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsInvalidCausationIds"`
Expected: FAIL because `EventContext<TEvent>` currently accepts invalid causation ids.

- [ ] **Step 3: Write minimal implementation**

Use `EventCorrelationIds.ValidateOptional` for `EventContext<TEvent>.CausationId`. Update `api-contracts.md` so `EventContext<TEvent>` documents all hardened constructor boundaries, and add a 2026-06-14 implementation-plan note for this batch.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventContextRejectsInvalidCausationIds"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): validate context causation ids`
