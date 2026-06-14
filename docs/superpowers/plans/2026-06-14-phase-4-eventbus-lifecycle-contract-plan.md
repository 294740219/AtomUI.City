# EventBus Lifecycle Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Harden `AtomUI.City.EventBus` lifecycle and contract boundaries so disposed buses reject runtime operations and contract descriptors reject default ids.

**Architecture:** Add a bus-level disposed flag to `InMemoryEventBus`, then guard publish, post, and subscription entry points with the smallest checks that preserve existing null/cancellation validation. Dispose active subscriptions outside the bus lock, expose disposal through DI and `IEventBus`, and keep contract descriptor validation at construction boundaries.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `Microsoft.Extensions.DependencyInjection`, `AtomUI.City.EventBus`, `engineering/check-docs.sh`, `engineering/check-public-api.sh`, `git diff --check`.

---

## File Structure

- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
  - Add `_disposed`, `Dispose`, `ThrowIfDisposed`, runtime guards, and subscription cleanup.
  - Later implement `IDisposable` after DI disposal is tested.
- Modify: `src/AtomUI.City.EventBus/IEventBus.cs`
  - Add `IDisposable` to the combined bus contract after concrete behavior is proven.
- Modify: `src/AtomUI.City.EventBus/EventContractDescriptor.cs`
  - Validate plugin-private descriptors reject default `EventContractId`.
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
  - Add bus lifecycle tests.
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
  - Add publish/post disposed tests.
- Modify: `tests/AtomUI.City.EventBus.Tests/EventBusRegistrationTests.cs`
  - Add DI disposal test.
- Modify: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`
  - Add plugin-private descriptor boundary test.
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/features.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/lifecycle.md`
- Modify: `docs/modules/eventbus/testing.md`
- Modify: `docs/modules/eventbus/contracts.md`

---

### Task 1: InMemoryEventBus Dispose Idempotency

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DisposeCanBeCalledMoreThanOnce()
{
    var eventBus = new InMemoryEventBus();

    eventBus.Dispose();
    eventBus.Dispose();
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DisposeCanBeCalledMoreThanOnce"`

Expected: FAIL because `InMemoryEventBus` has no public `Dispose` method.

- [x] **Step 3: Write minimal implementation**

Add a private `_disposed` field and a public idempotent `Dispose` method that sets `_disposed` under `_syncRoot`. Do not implement `IDisposable` yet.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DisposeCanBeCalledMoreThanOnce"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): make bus dispose idempotent`

### Task 2: PublishAsync Rejects Disposed Bus

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task PublishAsyncRejectsDisposedBus()
{
    var eventBus = new InMemoryEventBus();

    eventBus.Dispose();

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        await eventBus.PublishAsync(new TestEvent("disposed")));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishAsyncRejectsDisposedBus"`

Expected: FAIL because `PublishAsync` still succeeds on a disposed bus.

- [x] **Step 3: Write minimal implementation**

Add `ThrowIfDisposed` and call it in `PublishAsync` after null and cancellation validation.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PublishAsyncRejectsDisposedBus"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject publish after dispose`

### Task 3: PostAsync Rejects Disposed Bus

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task PostAsyncRejectsDisposedBus()
{
    var eventBus = new InMemoryEventBus();

    eventBus.Dispose();

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        await eventBus.PostAsync(new TestEvent("disposed")));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostAsyncRejectsDisposedBus"`

Expected: FAIL because `PostAsync` still accepts work on a disposed bus.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed` in `PostAsync` after null validation and before normalizing options or touching the contract registry.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PostAsyncRejectsDisposedBus"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject post after dispose`

### Task 4: Subscribe Rejects Disposed Bus

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void SubscribeRejectsDisposedBus()
{
    var eventBus = new InMemoryEventBus();

    eventBus.Dispose();

    Assert.Throws<ObjectDisposedException>(
        () => eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~SubscribeRejectsDisposedBus"`

Expected: FAIL because the no-owner async subscribe overload still creates a subscription.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed` in the no-owner `Subscribe<TEvent>(Func<EventContext<TEvent>, ValueTask>, EventSubscriptionOptions?)` overload after handler validation.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~SubscribeRejectsDisposedBus"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject subscriptions after dispose`

### Task 5: Owned Subscribe Rejects Disposed Bus

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void OwnedSubscribeRejectsDisposedBus()
{
    var eventBus = new InMemoryEventBus();
    var owner = LifecycleScope.CreateRoot(LifecycleScopeKind.Application, "app");

    eventBus.Dispose();

    Assert.Throws<ObjectDisposedException>(
        () => eventBus.Subscribe<TestEvent>(
            owner,
            _ => ValueTask.CompletedTask));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~OwnedSubscribeRejectsDisposedBus"`

Expected: FAIL because the owner overload still calls `SubscribeCore`.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed` in the owner `Subscribe<TEvent>(LifecycleScope, Func<EventContext<TEvent>, ValueTask>, EventSubscriptionOptions?)` overload after owner and handler validation.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~OwnedSubscribeRejectsDisposedBus"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): reject owned subscriptions after dispose`

### Task 6: Dispose Clears Active Subscriptions

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DisposeClearsActiveSubscriptions()
{
    var eventBus = new InMemoryEventBus();
    var subscription = eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

    eventBus.Dispose();

    Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    subscription.Dispose();
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DisposeClearsActiveSubscriptions"`

Expected: FAIL because bus dispose does not stop existing subscriptions.

- [x] **Step 3: Write minimal implementation**

Inside `Dispose`, copy all active subscriptions under `_syncRoot`, clear `_subscriptions`, then dispose the copied subscriptions outside the lock.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~DisposeClearsActiveSubscriptions"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): dispose bus subscriptions`

### Task 7: ServiceProvider Disposal Disposes EventBus

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventBusRegistrationTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task ServiceProviderDisposesEventBusSingleton()
{
    var services = new ServiceCollection();
    services.AddEventBus();
    var serviceProvider = services.BuildServiceProvider();
    var eventBus = (InMemoryEventBus)serviceProvider.GetRequiredService<IEventBus>();

    await serviceProvider.DisposeAsync();

    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        await eventBus.PublishAsync(new RegisteredEvent("disposed")));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~ServiceProviderDisposesEventBusSingleton"`

Expected: FAIL because `InMemoryEventBus` has a `Dispose` method but is not yet `IDisposable`, so DI does not dispose it.

- [x] **Step 3: Write minimal implementation**

Change `InMemoryEventBus` to implement `IDisposable`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~ServiceProviderDisposesEventBusSingleton"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): dispose bus from service provider`

### Task 8: IEventBus Exposes Dispose Contract

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventBusRegistrationTests.cs`
- Modify: `src/AtomUI.City.EventBus/IEventBus.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void EventBusInterfaceExposesDisposeContract()
{
    Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IEventBus)));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventBusInterfaceExposesDisposeContract"`

Expected: FAIL because only the concrete bus is disposable.

- [x] **Step 3: Write minimal implementation**

Change `IEventBus` to inherit `IDisposable`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~EventBusInterfaceExposesDisposeContract"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): expose bus disposal contract`

### Task 9: PluginPrivate Descriptor Rejects Default Contract Id

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`
- Modify: `src/AtomUI.City.EventBus/EventContractDescriptor.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void PluginPrivateContractDescriptorRejectsDefaultContractId()
{
    Assert.Throws<ArgumentException>(
        () => EventContractDescriptor.PluginPrivate<TestEvent>(default));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PluginPrivateContractDescriptorRejectsDefaultContractId"`

Expected: FAIL because `PluginPrivate` currently accepts a default `EventContractId`.

- [x] **Step 3: Write minimal implementation**

Call `EventContractId.ThrowIfDefault(contractId, nameof(contractId))` inside `EventContractDescriptor.PluginPrivate<TEvent>`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter "FullyQualifiedName~PluginPrivateContractDescriptorRejectsDefaultContractId"`

- [x] **Step 5: Commit**

Commit message: `fix(EventBus): validate plugin-private contract id`

### Task 10: Document EventBus Lifecycle Contract

**Files:**
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/features.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/lifecycle.md`
- Modify: `docs/modules/eventbus/testing.md`
- Modify: `docs/modules/eventbus/contracts.md`
- Modify: `docs/superpowers/plans/2026-06-14-phase-4-eventbus-lifecycle-contract-plan.md`

- [x] **Step 1: Update docs**

Document that `IEventBus` / `InMemoryEventBus` expose an idempotent `Dispose`; disposed buses reject publish, post, and subscription APIs with `ObjectDisposedException`; DI provider disposal releases the singleton; plugin-private contract descriptors reject default ids.

- [x] **Step 2: Run module and docs gates**

Run:

```bash
dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [x] **Step 3: Commit**

Commit message: `docs(EventBus): document bus lifecycle contract`

## Final Verification

After Task 10, run:

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
git status --short
```

Expected: build succeeds, all tests pass, docs/public API/diff checks pass, and worktree is clean.
