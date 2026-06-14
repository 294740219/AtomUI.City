# State Collection Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Harden `StateCollection<TKey,TItem>` lifecycle behavior so collection state has explicit, tested dispose boundaries.

**Architecture:** Keep the existing collection state API shape and add a small disposed flag guarded by the existing `_syncRoot`. Mutating and subscription APIs reject work after dispose; read/snapshot APIs remain available, mirroring `WritableState<T>` lifecycle semantics. Existing subscriptions are disposed and removed during collection disposal.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `AtomUI.City.State`, `engineering/check-docs.sh`, `engineering/check-public-api.sh`, `git diff --check`.

---

## File Structure

- Modify: `src/AtomUI.City.State/StateCollection.cs`
  - Add `IDisposable`, `_disposed`, `Dispose`, and a small `ThrowIfDisposed` helper.
  - Guard mutating APIs and `OnChange`.
  - Dispose and clear active subscriptions.
- Modify: `src/AtomUI.City.State/IStateCollection.cs`
  - Expose the disposal contract on the collection interface.
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
  - Add one product-contract test per lifecycle boundary.
- Modify: `docs/modules/state/api-contracts.md`
  - Document collection dispose behavior.
- Modify: `docs/modules/state/collection-state.md`
  - Add collection lifecycle testing matrix rows.
- Modify: `docs/modules/state/features.md`
  - Track disposed collection failure behavior.
- Modify: `docs/modules/state/implementation-plan.md`
  - Record the 2026-06-14 collection lifecycle hardening batch.
- Modify: `docs/modules/state/lifecycle.md`
  - Include `StateCollection` in the module state machine.
- Modify: `docs/modules/state/testing.md`
  - Include disposed collection assertions in AUC-STATE-006.

---

### Task 1: StateCollection Dispose Idempotency

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DisposeCanBeCalledMoreThanOnce()
{
    var collection = new StateCollection<string, int>();
    collection.AddOrUpdate("settings", 1);

    collection.Dispose();
    collection.Dispose();

    Assert.Equal(1, collection.Version);
    Assert.Equal(1, collection.Items["settings"]);
    Assert.True(collection.TryGetItemVersion("settings", out var itemVersion));
    Assert.Equal(1, itemVersion);
    Assert.Equal(1, collection.CreateSnapshot().ItemCount);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~DisposeCanBeCalledMoreThanOnce"`

Expected: FAIL because `StateCollection<TKey,TItem>` has no public `Dispose` method.

- [x] **Step 3: Write minimal implementation**

Make `StateCollection<TKey,TItem>` implement `IDisposable`; add `_disposed`; add idempotent `Dispose` that marks the collection disposed. Do not guard read APIs.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~DisposeCanBeCalledMoreThanOnce"`

- [x] **Step 5: Commit**

Commit message: `fix(State): make collection dispose idempotent`

### Task 2: AddOrUpdate Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void AddOrUpdateRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(() => collection.AddOrUpdate("settings", 1));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~AddOrUpdateRejectsDisposedCollection"`

Expected: FAIL because `AddOrUpdate` still mutates disposed collections.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `AddOrUpdate` lock before reading or mutating `_items`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~AddOrUpdateRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection add after dispose`

### Task 3: AddOrUpdateRange Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void AddOrUpdateRangeRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(
        () => collection.AddOrUpdateRange([new KeyValuePair<string, int>("settings", 1)]));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~AddOrUpdateRangeRejectsDisposedCollection"`

Expected: FAIL because range mutation still succeeds after dispose.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `AddOrUpdateRange` lock before building `nextItems`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~AddOrUpdateRangeRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection range add after dispose`

### Task 4: Remove Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void RemoveRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    collection.AddOrUpdate("settings", 1);
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(() => collection.Remove("settings"));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~RemoveRejectsDisposedCollection"`

Expected: FAIL because remove still mutates disposed collections.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `Remove` lock before checking `_items`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~RemoveRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection remove after dispose`

### Task 5: Clear Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void ClearRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    collection.AddOrUpdate("settings", 1);
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(() => collection.Clear());
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~ClearRejectsDisposedCollection"`

Expected: FAIL because clear still mutates disposed collections.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `Clear` lock before checking `_items.Count`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~ClearRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection clear after dispose`

### Task 6: RestoreSnapshot Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void RestoreSnapshotRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    var snapshot = new StateCollectionSnapshot<string, int>(
        collectionVersion: 1,
        [new StateCollectionSnapshotEntry<string, int>("settings", 1, ItemVersion: 1)]);
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(() => collection.RestoreSnapshot(snapshot));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~RestoreSnapshotRejectsDisposedCollection"`

Expected: FAIL because restore-style mutation still succeeds after dispose.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `RestoreSnapshot` lock after null validation and before building `nextItems`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~RestoreSnapshotRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection restore after dispose`

### Task 7: OnChange Rejects Disposed Collection

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void OnChangeRejectsDisposedCollection()
{
    var collection = new StateCollection<string, int>();
    collection.Dispose();

    Assert.Throws<ObjectDisposedException>(() => collection.OnChange(_ => { }));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~OnChangeRejectsDisposedCollection"`

Expected: FAIL because subscriptions can still be created after dispose.

- [x] **Step 3: Write minimal implementation**

Call `ThrowIfDisposed()` inside the `OnChange` lock before adding the new subscription.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~OnChangeRejectsDisposedCollection"`

- [x] **Step 5: Commit**

Commit message: `fix(State): reject collection subscriptions after dispose`

### Task 8: Dispose Clears Active Subscriptions

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/StateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void DisposeClearsActiveSubscriptions()
{
    var collection = new StateCollection<string, int>();
    var subscription = collection.OnChange(_ => { });

    collection.Dispose();

    var subscriptions = Assert.IsAssignableFrom<System.Collections.ICollection>(
        typeof(StateCollection<string, int>)
            .GetField("_subscriptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(collection));
    Assert.Empty(subscriptions);
    subscription.Dispose();
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~DisposeClearsActiveSubscriptions"`

Expected: FAIL because collection disposal has not cleared active subscriptions yet.

- [x] **Step 3: Write minimal implementation**

Ensure collection `Dispose` disposes the copied subscription list outside the lock, clears `_subscriptions`, and `RemovingStateSubscription.Dispose` remains idempotent after collection disposal.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~DisposeClearsActiveSubscriptions"`

- [x] **Step 5: Commit**

Commit message: `fix(State): dispose collection subscriptions`

### Task 9: IStateCollection Exposes Dispose Contract

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`
- Modify: `src/AtomUI.City.State/IStateCollection.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void CollectionInterfaceExposesDisposeContract()
{
    Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IStateCollection<string, int>)));
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionInterfaceExposesDisposeContract"`

Expected: FAIL because only the concrete collection type implements `IDisposable`.

- [x] **Step 3: Write minimal implementation**

Change `IStateCollection<TKey,TItem>` to inherit `IDisposable`.

- [x] **Step 4: Run focused test**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionInterfaceExposesDisposeContract"`

- [x] **Step 5: Commit**

Commit message: `fix(State): expose collection disposal contract`

### Task 10: Document Collection Lifecycle Contract

**Files:**
- Modify: `docs/modules/state/api-contracts.md`
- Modify: `docs/modules/state/collection-state.md`
- Modify: `docs/modules/state/features.md`
- Modify: `docs/modules/state/implementation-plan.md`
- Modify: `docs/modules/state/lifecycle.md`
- Modify: `docs/modules/state/testing.md`
- Modify: `docs/superpowers/plans/2026-06-14-phase-4-state-collection-lifecycle-plan.md`

- [x] **Step 1: Update docs**

Document that `StateCollection<TKey,TItem>` has an idempotent `Dispose`; disposed collections reject mutation, restore, and subscription APIs with `ObjectDisposedException`; read and snapshot APIs remain available.

- [x] **Step 2: Run docs and module gates**

Run:

```bash
dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [x] **Step 3: Commit**

Commit message: `docs(State): document collection lifecycle`

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
