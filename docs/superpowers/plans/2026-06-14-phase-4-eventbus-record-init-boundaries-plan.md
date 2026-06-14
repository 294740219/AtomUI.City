# EventBus Record Init Boundary Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden EventBus public record result types so `with { ... }` init mutations cannot bypass constructor validation.

**Architecture:** Keep the existing `EventDeliveryResult` and `EventPostResult` public shapes, constructor parameter names, and record semantics. Add backing fields with validating `init` setters only where a task proves an init mutation gap. Tests stay in `EventPublicationTests` and each task covers one public boundary.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `engineering/check-docs.sh`, `engineering/check-public-api.sh`, `git diff --check`.

---

## Tasks

### Task 1: Reject default delivery subscription ids in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsDefaultSubscriptionIdInitMutation()
{
    var delivery = new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true);

    Assert.Throws<ArgumentException>(() => delivery with
    {
        SubscriptionId = default,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsDefaultSubscriptionIdInitMutation"`
Expected: FAIL because the current `init` mutation bypasses `ValidateSubscriptionId`.

- [x] **Step 3: Write minimal implementation**

Change `EventDeliveryResult.SubscriptionId` to a validating backing field:

```csharp
private EventSubscriptionId _subscriptionId = ValidateSubscriptionId(SubscriptionId);

public EventSubscriptionId SubscriptionId
{
    get => _subscriptionId;
    init => _subscriptionId = ValidateSubscriptionId(value);
}
```

Update `api-contracts.md` so the `EventDeliveryResult constructor` row says constructor and init mutations reject default subscription ids.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsDefaultSubscriptionIdInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate delivery subscription init`

### Task 2: Reject unknown delivery dispatch policies in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsUnknownDispatchPolicyInitMutation()
{
    var delivery = new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true);

    Assert.Throws<ArgumentOutOfRangeException>(() => delivery with
    {
        DispatchPolicy = (EventDispatchPolicy)999,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsUnknownDispatchPolicyInitMutation"`
Expected: FAIL because the current `init` mutation bypasses `ValidateDispatchPolicy`.

- [x] **Step 3: Write minimal implementation**

Change `EventDeliveryResult.DispatchPolicy` to a validating backing field:

```csharp
private EventDispatchPolicy _dispatchPolicy = ValidateDispatchPolicy(DispatchPolicy);

public EventDispatchPolicy DispatchPolicy
{
    get => _dispatchPolicy;
    init => _dispatchPolicy = ValidateDispatchPolicy(value);
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsUnknownDispatchPolicyInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate delivery dispatch init`

### Task 3: Reject successful canceled delivery init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsSuccessfulCancellationInitMutation()
{
    var delivery = new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true);

    Assert.Throws<ArgumentException>(() => delivery with
    {
        Canceled = true,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulCancellationInitMutation"`
Expected: FAIL because `Canceled` init mutations do not re-check the success/cancel invariant.

- [x] **Step 3: Write minimal implementation**

Add a validating `Canceled` backing field:

```csharp
private bool _canceled = ValidateCanceled(Succeeded, Canceled);

public bool Canceled
{
    get => _canceled;
    init => _canceled = ValidateCanceled(Succeeded, value);
}

private static bool ValidateCanceled(bool succeeded, bool canceled)
{
    if (succeeded && canceled)
    {
        throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Canceled));
    }

    return canceled;
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulCancellationInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate delivery canceled init`

### Task 4: Reject successful delivery error init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsSuccessfulErrorMessageInitMutation()
{
    var delivery = new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: true);

    Assert.Throws<ArgumentException>(() => delivery with
    {
        ErrorMessage = "should not be present",
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulErrorMessageInitMutation"`
Expected: FAIL because `ErrorMessage` init mutations do not re-check the success/error invariant.

- [x] **Step 3: Write minimal implementation**

Add a validating `ErrorMessage` backing field:

```csharp
private string? _errorMessage = ValidateErrorMessage(Succeeded, ErrorMessage);

public string? ErrorMessage
{
    get => _errorMessage;
    init => _errorMessage = ValidateErrorMessage(Succeeded, value);
}

private static string? ValidateErrorMessage(bool succeeded, string? errorMessage)
{
    if (succeeded && !string.IsNullOrWhiteSpace(errorMessage))
    {
        throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
    }

    return errorMessage;
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSuccessfulErrorMessageInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate delivery error init`

### Task 5: Reject succeeded init mutation on failed delivery with error

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DeliveryResultRejectsSucceededInitMutationWithErrorMessage()
{
    var delivery = new EventDeliveryResult(
        EventSubscriptionId.New(),
        EventDispatchPolicy.Serialized,
        Succeeded: false,
        ErrorMessage: "boom");

    Assert.Throws<ArgumentException>(() => delivery with
    {
        Succeeded = true,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSucceededInitMutationWithErrorMessage"`
Expected: FAIL because `Succeeded` init mutations do not re-check existing error state.

- [x] **Step 3: Write minimal implementation**

Change `Succeeded` to a backing field that validates against current `Canceled` and `ErrorMessage`:

```csharp
private bool _succeeded = ValidateSucceeded(Succeeded, Canceled, ErrorMessage);

public bool Succeeded
{
    get => _succeeded;
    init => _succeeded = ValidateSucceeded(value, Canceled, ErrorMessage);
}
```

Update `api-contracts.md` so `EventDeliveryResult` documents that constructor and init mutations both enforce delivery-state consistency.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DeliveryResultRejectsSucceededInitMutationWithErrorMessage"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate delivery succeeded init`

### Task 6: Reject empty post event ids in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsEmptyEventIdInitMutation()
{
    var result = new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.post.v1"),
        Accepted: true);

    Assert.Throws<ArgumentException>(() => result with
    {
        EventId = Guid.Empty,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsEmptyEventIdInitMutation"`
Expected: FAIL because `EventId` init mutations bypass `ValidateEventId`.

- [x] **Step 3: Write minimal implementation**

Change `EventPostResult.EventId` to a validating backing field:

```csharp
private Guid _eventId = ValidateEventId(EventId);

public Guid EventId
{
    get => _eventId;
    init => _eventId = ValidateEventId(value);
}
```

Update `api-contracts.md` so `EventPostResult` documents constructor and init mutation validation for event id.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsEmptyEventIdInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate post event init`

### Task 7: Reject default post contract ids in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsDefaultContractIdInitMutation()
{
    var result = new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.post.v1"),
        Accepted: true);

    Assert.Throws<ArgumentException>(() => result with
    {
        ContractId = default,
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsDefaultContractIdInitMutation"`
Expected: FAIL because `ContractId` init mutations bypass `ValidateContractId`.

- [x] **Step 3: Write minimal implementation**

Change `EventPostResult.ContractId` to a validating backing field:

```csharp
private EventContractId _contractId = ValidateContractId(ContractId);

public EventContractId ContractId
{
    get => _contractId;
    init => _contractId = ValidateContractId(value);
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsDefaultContractIdInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate post contract init`

### Task 8: Reject accepted post rejection reasons in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsAcceptedRejectionReasonInitMutation()
{
    var result = new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.post.v1"),
        Accepted: true);

    Assert.Throws<ArgumentException>(() => result with
    {
        RejectionReason = "not rejected",
    });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsAcceptedRejectionReasonInitMutation"`
Expected: FAIL because `RejectionReason` init mutations do not re-check accepted state.

- [x] **Step 3: Write minimal implementation**

Change `RejectionReason` to a validating backing field:

```csharp
private string? _rejectionReason = ValidateRejectionReason(Accepted, RejectionReason);

public string? RejectionReason
{
    get => _rejectionReason;
    init => _rejectionReason = ValidateRejectionReason(Accepted, value);
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsAcceptedRejectionReasonInitMutation"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate post rejection init`

### Task 9: Reject rejected post results without reasons in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsRejectedMissingReasonInitMutation()
{
    var result = new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.post.v1"),
        Accepted: true);

    Assert.Throws<ArgumentException>(() => result with
    {
        Accepted = false,
    });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsRejectedMissingReasonInitMutation"`
Expected: FAIL because `Accepted` init mutations do not require a rejection reason.

- [ ] **Step 3: Write minimal implementation**

Add a validating `Accepted` backing field that rejects the rejected-without-reason state:

```csharp
private bool _accepted = ValidateAccepted(Accepted, RejectionReason);

public bool Accepted
{
    get => _accepted;
    init => _accepted = ValidateAccepted(value, RejectionReason);
}

private static bool ValidateAccepted(bool accepted, string? rejectionReason)
{
    if (!accepted && string.IsNullOrWhiteSpace(rejectionReason))
    {
        throw new ArgumentException("Rejected event post result must include a rejection reason.", nameof(Accepted));
    }

    return accepted;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsRejectedMissingReasonInitMutation"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): validate post accepted init`

### Task 10: Reject accepted post results that keep rejection reasons in init mutations

**Files:**
- Modify: `src/AtomUI.City.EventBus/EventPostResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Test: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: this plan file

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void PostResultRejectsAcceptedReasonStateInitMutation()
{
    var result = new EventPostResult(
        Guid.NewGuid(),
        new EventContractId("atomui.city.tests.post.v1"),
        Accepted: false,
        RejectionReason: "not accepted");

    Assert.Throws<ArgumentException>(() => result with
    {
        Accepted = true,
    });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsAcceptedReasonStateInitMutation"`
Expected: FAIL because `Accepted` init mutations only reject missing rejection reasons after Task 9.

- [ ] **Step 3: Write minimal implementation**

Extend `ValidateAccepted` so it also rejects accepted results that still carry a rejection reason:

```csharp
if (accepted && !string.IsNullOrWhiteSpace(rejectionReason))
{
    throw new ArgumentException("Accepted event post result cannot include a rejection reason.", nameof(Accepted));
}
```

Update `api-contracts.md` so `EventPostResult` documents constructor and init mutation consistency. Add a 2026-06-14 note to `implementation-plan.md` that `EventDeliveryResult` and `EventPostResult` init mutation boundaries are hardened.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostResultRejectsAcceptedReasonStateInitMutation"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(EventBus): validate post accepted reason init`

---

## Final Verification

- [ ] Run `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj`.
- [ ] Run `dotnet build AtomUICity.slnx`.
- [ ] Run `dotnet test AtomUICity.slnx --no-build`.
- [ ] Run `bash engineering/check-docs.sh`.
- [ ] Run `bash engineering/check-public-api.sh`.
- [ ] Run `git diff --check`.
