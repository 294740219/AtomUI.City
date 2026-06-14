# EventBus Result Boundary Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden AtomUI.City.EventBus public contract/result boundaries so default struct ids, empty event ids, invalid dispatch policies, and inconsistent result states cannot enter product APIs.

**Architecture:** Keep the existing EventBus in-memory implementation and add narrow constructor/boundary validation to public value/result types. Tests stay in the existing EventBus test project and focus on one invariant per task so every commit has a clear product-contract reason.

**Tech Stack:** .NET, xUnit, Microsoft.Extensions.DependencyInjection, AtomUI.City.EventBus.

---

### Task 1: Reject default event contract ids in descriptors

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventContractId.cs`
- Modify: `src/AtomUI.City.EventBus/EventContractDescriptor.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void SharedContractDescriptorRejectsDefaultContractId()
{
    Assert.Throws<ArgumentException>(
        () => EventContractDescriptor.Shared<TestEvent>(default, typeof(TestEvent).Assembly));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~SharedContractDescriptorRejectsDefaultContractId"`
Expected: FAIL because default `EventContractId` is accepted.

- [x] **Step 3: Write minimal implementation**

Add an internal `EventContractId.ThrowIfDefault` guard and call it from `EventContractDescriptor.Shared`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~SharedContractDescriptorRejectsDefaultContractId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject default contract ids in descriptors`

### Task 2: Reject empty event ids in publish results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PublishResultRejectsEmptyEventId()
{
    Assert.Throws<ArgumentException>(() => new EventPublishResult(
        Guid.Empty,
        new EventContractId("atomui.city.tests.event.v1"),
        []));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishResultRejectsEmptyEventId"`
Expected: FAIL because `Guid.Empty` is accepted.

- [x] **Step 3: Write minimal implementation**

Throw `ArgumentException` when `eventId == Guid.Empty`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishResultRejectsEmptyEventId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject empty publish result ids`

### Task 3: Reject default contract ids in publish results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PublishResultRejectsDefaultContractId()
{
    Assert.Throws<ArgumentException>(() => new EventPublishResult(
        Guid.NewGuid(),
        default,
        []));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishResultRejectsDefaultContractId"`
Expected: FAIL because default `EventContractId` is accepted.

- [x] **Step 3: Write minimal implementation**

Call `EventContractId.ThrowIfDefault(contractId)` in `EventPublishResult`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishResultRejectsDefaultContractId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject default publish contract ids`

### Task 4: Reject default subscription ids in delivery results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventSubscriptionId.cs`
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsDefaultSubscriptionId()
{
    Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
        default,
        EventDispatchPolicy.Serialized,
        Succeeded: true));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsDefaultSubscriptionId"`
Expected: FAIL because default `EventSubscriptionId` is accepted.

- [x] **Step 3: Write minimal implementation**

Add an internal `EventSubscriptionId.ThrowIfDefault` guard and validate the `EventDeliveryResult.SubscriptionId` primary property.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsDefaultSubscriptionId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject default delivery subscription ids`

### Task 5: Reject unknown dispatch policies in delivery results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsUnknownDispatchPolicy()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new EventDeliveryResult(
        EventSubscriptionId.New(),
        (EventDispatchPolicy)999,
        Succeeded: true));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsUnknownDispatchPolicy"`
Expected: FAIL because unknown dispatch policy is accepted.

- [x] **Step 3: Write minimal implementation**

Validate `Enum.IsDefined(dispatchPolicy)` for `EventDeliveryResult`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsUnknownDispatchPolicy"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject unknown delivery dispatch policies`

### Task 6: Reject delivery results that are both succeeded and canceled

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsSuccessfulCancellation()
{
    Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true,
        Canceled: true));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulCancellation"`
Expected: FAIL because contradictory delivery status is accepted.

- [x] **Step 3: Write minimal implementation**

Validate that `Succeeded` and `Canceled` are not both `true`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulCancellation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject contradictory delivery status`

### Task 7: Reject successful delivery results with error messages

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsSuccessfulErrorMessage()
{
    Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true,
        ErrorMessage: "should not be present"));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulErrorMessage"`
Expected: FAIL because successful delivery can carry an error message.

- [x] **Step 3: Write minimal implementation**

Validate that successful delivery results have no non-empty error message.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulErrorMessage"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject successful delivery errors`

### Task 8: Reject empty event ids in post results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsEmptyEventId()
{
    Assert.Throws<ArgumentException>(() => new EventPostResult(
        Guid.Empty,
        new EventContractId("atomui.city.tests.event.v1"),
        Accepted: true));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsEmptyEventId"`
Expected: FAIL because `Guid.Empty` is accepted.

- [x] **Step 3: Write minimal implementation**

Validate the `EventPostResult.EventId` primary property.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsEmptyEventId"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject empty post result ids`

### Task 9: Reject default contract ids in post results

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsDefaultContractId()
{
    Assert.Throws<ArgumentException>(() => new EventPostResult(
        Guid.NewGuid(),
        default,
        Accepted: true));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsDefaultContractId"`
Expected: FAIL because default `EventContractId` is accepted.

- [ ] **Step 3: Write minimal implementation**

Call `EventContractId.ThrowIfDefault(contractId)` in `EventPostResult`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsDefaultContractId"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): reject default post contract ids`

### Task 10: Enforce post result rejection reason consistency

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/testing.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultEnforcesRejectionReasonConsistency()
{
    Assert.Throws<ArgumentException>(() => new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.event.v1"),
        Accepted: true,
        RejectionReason: "not rejected"));

    Assert.Throws<ArgumentException>(() => new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.event.v1"),
        Accepted: false));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultEnforcesRejectionReasonConsistency"`
Expected: FAIL because post results can mix accepted/rejected state and reason.

- [ ] **Step 3: Write minimal implementation**

Validate accepted results have no rejection reason and rejected results include one; then update EventBus docs and matrix for result boundary hardening.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultEnforcesRejectionReasonConsistency"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): enforce post result consistency`

---

## Final Verification

- [ ] Run `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj`.
- [ ] Run `dotnet build AtomUICity.slnx`.
- [ ] Run `dotnet test AtomUICity.slnx --no-build`.
- [ ] Run `bash engineering/check-docs.sh`.
- [ ] Run `bash engineering/check-public-api.sh`.
- [ ] Run `git diff --check`.
