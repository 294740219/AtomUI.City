# Phase 4 Writable State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring `AUC-STATE-001 Writable State` to a focused product-contract slice covering deterministic versioning, committed-before-notify semantics, subscription disposal, and disposed-state mutation rejection.

**Architecture:** Keep `WritableState<T>` as the single state value implementation and do not change the `IWritableState<T>` interface. Add disposal behavior directly to `WritableState<T>` so mutating APIs reject use after disposal while read-only properties remain observable for diagnostics. Keep notification dispatch outside the state lock.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `IHostDiagnostics`, `engineering/check-docs.sh`, `engineering/check-public-api.sh`.

---

## Tasks

### Task 1: Add failing tests for writable state product contract

**Files:**
- Modify: `tests/AtomUI.City.State.Tests/WritableStateTests.cs`

- [x] **Step 1: Add tests for version, committed-before-notify, subscription disposal, and disposed mutation rejection**

Add these tests to `WritableStateTests`:

```csharp
[Fact]
public void SetValueIncrementsVersionAndNotifiesAfterCommit()
{
    var state = new WritableState<int>(1);
    long observedVersion = -1;
    int observedValue = -1;
    StateChangedEventArgs<int>? observedArgs = null;

    state.OnChange(args =>
    {
        observedVersion = state.Version;
        observedValue = state.Value;
        observedArgs = args;
    });

    var changed = state.SetValue(2);

    Assert.True(changed);
    Assert.Equal(1, state.Version);
    Assert.Equal(1, observedVersion);
    Assert.Equal(2, observedValue);
    Assert.NotNull(observedArgs);
    Assert.Equal(1, observedArgs.OldValue);
    Assert.Equal(2, observedArgs.NewValue);
    Assert.Equal(1, observedArgs.Version);
}

[Fact]
public void DisposedSubscriptionStopsReceivingNotifications()
{
    var state = new WritableState<int>(0);
    var notifications = 0;
    var subscription = state.OnChange(_ => notifications++);

    subscription.Dispose();
    state.SetValue(1);

    Assert.Equal(0, notifications);
}

[Fact]
public void DisposeRejectsMutatingApisAndKeepsReadPropertiesAvailable()
{
    var state = new WritableState<int>(3);

    state.Dispose();
    state.Dispose();

    Assert.Equal(3, state.Value);
    Assert.Equal(0, state.Version);
    Assert.Throws<ObjectDisposedException>(() => state.SetValue(4));
    Assert.Throws<ObjectDisposedException>(() => state.Set(4));
    Assert.Throws<ObjectDisposedException>(() => state.Update(value => value + 1));
    Assert.Throws<ObjectDisposedException>(() => state.OnChange(_ => { }));
}
```

- [x] **Step 2: Run the focused tests and verify the dispose test fails**

Run:

```bash
dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter WritableStateTests
```

Expected: `DisposeRejectsMutatingApisAndKeepsReadPropertiesAvailable` fails to compile or fails because `WritableState<T>` does not expose product-level disposal behavior yet.

### Task 2: Implement writable state disposal

**Files:**
- Modify: `src/AtomUI.City.State/WritableState.cs`

- [x] **Step 1: Implement `IDisposable` on `WritableState<T>`**

Change the class declaration and add `_disposed`:

```csharp
public sealed class WritableState<T> : IWritableState<T>, IDisposable
{
    private bool _disposed;
```

- [x] **Step 2: Guard mutating APIs**

Call `ThrowIfDisposed()` at the start of `SetValue`, `Update`, both `OnChange` overloads, and internal `Restore`.

- [x] **Step 3: Dispose subscriptions idempotently**

Add a public `Dispose` method that snapshots and clears `_subscriptions` under `_syncRoot`, then disposes each subscription outside the lock.

- [x] **Step 4: Add a private disposal guard**

Add:

```csharp
private void ThrowIfDisposed()
{
    if (_disposed)
    {
        throw new ObjectDisposedException(GetType().FullName);
    }
}
```

- [x] **Step 5: Run focused tests and verify they pass**

Run:

```bash
dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter WritableStateTests
```

Expected: all `WritableStateTests` pass.

### Task 3: Sync state documentation and plan status

**Files:**
- Modify: `docs/modules/state/api-contracts.md`
- Modify: `docs/modules/state/features.md`
- Modify: `docs/modules/state/implementation-plan.md`
- Modify: `docs/modules/state/state-values.md`
- Modify: `docs/modules/state/testing.md`
- Modify: `docs/superpowers/plans/2026-06-14-phase-4-writable-state-plan.md`

- [x] **Step 1: Update API and feature docs**

Document that `WritableState<T>` read properties remain available after disposal, while `Set`, `SetValue`, `Update`, `OnChange`, and restore-style mutations reject disposed state with `ObjectDisposedException`.

- [x] **Step 2: Update implementation status**

Set `AUC-STATE-001` to productization in progress with product contract tests partially passing, not fully implemented.

- [x] **Step 3: Mark this plan's completed tasks**

Update checkboxes in this file as each task completes.

### Task 4: Run gates and commit

**Files:**
- Check: all files changed by Tasks 1-3.

- [x] **Step 1: Run focused state tests**

```bash
dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj
```

- [x] **Step 2: Run full verification**

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [x] **Step 3: Commit**

```bash
git add docs/modules/state/api-contracts.md docs/modules/state/features.md docs/modules/state/implementation-plan.md docs/modules/state/state-values.md docs/modules/state/testing.md docs/superpowers/plans/2026-06-14-phase-4-writable-state-plan.md src/AtomUI.City.State/WritableState.cs tests/AtomUI.City.State.Tests/WritableStateTests.cs
git commit -m "feat(State): harden writable state lifecycle"
```
