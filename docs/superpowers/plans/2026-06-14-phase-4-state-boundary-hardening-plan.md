# State Boundary Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden AtomUI.City.State public value, snapshot, and collection contract boundaries so default keys and record `with` init mutations cannot bypass documented invariants.

**Architecture:** Keep existing State runtime types and add narrow validation at public construction/init boundaries. Tests stay in `tests/AtomUI.City.State.Tests` and each task proves one product-contract invariant with a red/green cycle.

**Tech Stack:** .NET, xUnit, AtomUI.City.State.

---

### Task 1: Reject default state keys in state definitions

**Files:**
- Modify: `src/AtomUI.City.State/StateKey.cs`
- Modify: `src/AtomUI.City.State/StateDefinition.cs`
- Modify: `docs/modules/state/api-contracts.md`
- Test: `tests/AtomUI.City.State.Tests/StateDefinitionTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void StateDefinitionRejectsDefaultKey()
{
    var exception = Assert.Throws<ArgumentException>(() =>
        StateDefinition.Create(default(StateKey<string>), "value"));

    Assert.Equal("key", exception.ParamName);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~StateDefinitionRejectsDefaultKey"`
Expected: FAIL because the default key reaches the base constructor with the wrong argument boundary.

- [x] **Step 3: Write minimal implementation**

Add `StateKey<T>.ThrowIfDefault(StateKey<T> key, string? paramName = null)` and call it from `StateDefinition<T>.Create` before constructing the definition. Update `api-contracts.md` so `StateDefinition.Create` documents the default-key failure.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~StateDefinitionRejectsDefaultKey"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): reject default keys in definitions`

### Task 2: Reject default state keys in registry reads

**Files:**
- Modify: `src/AtomUI.City.State/ApplicationStateRegistry.cs`
- Modify: `docs/modules/state/api-contracts.md`
- Test: `tests/AtomUI.City.State.Tests/ApplicationStateTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void MissingApplicationStateRejectsDefaultKeyBeforeDiagnostics()
{
    var diagnostics = new InMemoryHostDiagnostics();
    var registry = new ApplicationStateRegistry(diagnostics);

    var exception = Assert.Throws<ArgumentException>(
        () => registry.Get(default(StateKey<int>)));

    Assert.Equal("key", exception.ParamName);
    Assert.Empty(diagnostics.Records);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~MissingApplicationStateRejectsDefaultKeyBeforeDiagnostics"`
Expected: FAIL because registry lookup currently lets the default key reach dictionary lookup semantics.

- [x] **Step 3: Write minimal implementation**

Call `StateKey<T>.ThrowIfDefault(key, nameof(key))` at the start of `ApplicationStateRegistry.GetRegistration`. Update `api-contracts.md` to state application state registry key boundaries reject default keys before diagnostics.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~MissingApplicationStateRejectsDefaultKeyBeforeDiagnostics"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): reject default keys in registry reads`

### Task 3: Validate snapshot entry StateName init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateSnapshotEntry.cs`
- Modify: `docs/modules/state/api-contracts.md`
- Test: `tests/AtomUI.City.State.Tests/StateSnapshotTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
public void SnapshotEntryRejectsInvalidStateNameInit(string stateName)
{
    var entry = new StateSnapshotEntry(
        "AtomUI.City.Tests.Theme",
        typeof(string),
        "light",
        version: 0,
        schemaVersion: 1,
        ownerModule: null,
        pluginId: null);

    Assert.Throws<ArgumentException>(() => entry with { StateName = stateName });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsInvalidStateNameInit"`
Expected: FAIL because `with` init currently accepts invalid state names.

- [x] **Step 3: Write minimal implementation**

Back `StateSnapshotEntry.StateName` with an init setter that calls `ArgumentException.ThrowIfNullOrWhiteSpace`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsInvalidStateNameInit"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): validate snapshot entry state names`

### Task 4: Validate snapshot entry ValueType init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateSnapshotEntry.cs`
- Test: `tests/AtomUI.City.State.Tests/StateSnapshotTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void SnapshotEntryRejectsNullValueTypeInit()
{
    var entry = new StateSnapshotEntry(
        "AtomUI.City.Tests.Theme",
        typeof(string),
        "light",
        version: 0,
        schemaVersion: 1,
        ownerModule: null,
        pluginId: null);

    Assert.Throws<ArgumentNullException>(() => entry with { ValueType = null! });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNullValueTypeInit"`
Expected: FAIL because `with` init currently accepts null `ValueType`.

- [x] **Step 3: Write minimal implementation**

Back `StateSnapshotEntry.ValueType` with an init setter that calls `ArgumentNullException.ThrowIfNull`.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNullValueTypeInit"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): validate snapshot entry value types`

### Task 5: Validate snapshot entry Version init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateSnapshotEntry.cs`
- Test: `tests/AtomUI.City.State.Tests/StateSnapshotTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void SnapshotEntryRejectsNegativeVersionInit()
{
    var entry = new StateSnapshotEntry(
        "AtomUI.City.Tests.Theme",
        typeof(string),
        "light",
        version: 0,
        schemaVersion: 1,
        ownerModule: null,
        pluginId: null);

    Assert.Throws<ArgumentOutOfRangeException>(() => entry with { Version = -1 });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNegativeVersionInit"`
Expected: FAIL because `with` init currently accepts negative versions.

- [x] **Step 3: Write minimal implementation**

Back `StateSnapshotEntry.Version` with an init setter that rejects values below 0.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNegativeVersionInit"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): validate snapshot entry versions`

### Task 6: Validate snapshot entry SchemaVersion init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateSnapshotEntry.cs`
- Test: `tests/AtomUI.City.State.Tests/StateSnapshotTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
public void SnapshotEntryRejectsInvalidSchemaVersionInit(int schemaVersion)
{
    var entry = new StateSnapshotEntry(
        "AtomUI.City.Tests.Theme",
        typeof(string),
        "light",
        version: 0,
        schemaVersion: 1,
        ownerModule: null,
        pluginId: null);

    Assert.Throws<ArgumentOutOfRangeException>(() => entry with { SchemaVersion = schemaVersion });
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsInvalidSchemaVersionInit"`
Expected: FAIL because `with` init currently accepts invalid schema versions.

- [x] **Step 3: Write minimal implementation**

Back `StateSnapshotEntry.SchemaVersion` with an init setter that rejects values below 1. Update `api-contracts.md` to document that constructor and init boundaries share the same snapshot entry validation.

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsInvalidSchemaVersionInit"`
Expected: PASS.

- [x] **Step 5: Commit**

Commit message: `fix(State): validate snapshot entry schema versions`

### Task 7: Validate collection snapshot entry Key init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateCollectionSnapshotEntry.cs`
- Modify: `docs/modules/state/api-contracts.md`
- Test: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void SnapshotEntryRejectsNullKeyInit()
{
    var entry = new StateCollectionSnapshotEntry<string, int>("settings", 1, ItemVersion: 1);

    Assert.Throws<ArgumentNullException>(() => entry with { Key = null! });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNullKeyInit"`
Expected: FAIL because collection snapshot entry `with` init currently accepts null keys.

- [ ] **Step 3: Write minimal implementation**

Back `StateCollectionSnapshotEntry<TKey,TItem>.Key` with an init setter that calls `ArgumentNullException.ThrowIfNull`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNullKeyInit"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(State): validate collection snapshot keys`

### Task 8: Validate collection snapshot entry ItemVersion init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateCollectionSnapshotEntry.cs`
- Test: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void SnapshotEntryRejectsNegativeItemVersionInit()
{
    var entry = new StateCollectionSnapshotEntry<string, int>("settings", 1, ItemVersion: 1);

    Assert.Throws<ArgumentOutOfRangeException>(() => entry with { ItemVersion = -1 });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNegativeItemVersionInit"`
Expected: FAIL because collection snapshot entry `with` init currently accepts negative item versions.

- [ ] **Step 3: Write minimal implementation**

Back `StateCollectionSnapshotEntry<TKey,TItem>.ItemVersion` with an init setter that rejects values below 0. Update `api-contracts.md` to document that constructor and init boundaries share collection snapshot entry validation.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~SnapshotEntryRejectsNegativeItemVersionInit"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(State): validate collection snapshot versions`

### Task 9: Validate collection change Kind and Key init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateCollectionChange.cs`
- Modify: `docs/modules/state/api-contracts.md`
- Test: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void CollectionChangeRejectsInvalidKindInit()
{
    var change = new StateCollectionChange<string, int>(
        StateCollectionChangeKind.Added,
        "settings",
        HasOldItem: false,
        OldItem: default,
        HasNewItem: true,
        NewItem: 1,
        CollectionVersion: 1,
        ItemVersion: 1);

    Assert.Throws<ArgumentOutOfRangeException>(() => change with { Kind = (StateCollectionChangeKind)42 });
}

[Fact]
public void CollectionChangeRejectsNullKeyInit()
{
    var change = new StateCollectionChange<string, int>(
        StateCollectionChangeKind.Added,
        "settings",
        HasOldItem: false,
        OldItem: default,
        HasNewItem: true,
        NewItem: 1,
        CollectionVersion: 1,
        ItemVersion: 1);

    Assert.Throws<ArgumentNullException>(() => change with { Key = null! });
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionChangeRejectsInvalidKindInit|FullyQualifiedName~CollectionChangeRejectsNullKeyInit"`
Expected: FAIL because collection change `with` init currently accepts invalid kind and null key values.

- [ ] **Step 3: Write minimal implementation**

Back `StateCollectionChange<TKey,TItem>.Kind` and `Key` with init setters that enforce the constructor validation.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionChangeRejectsInvalidKindInit|FullyQualifiedName~CollectionChangeRejectsNullKeyInit"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(State): validate collection change identity`

### Task 10: Validate collection change version init mutations

**Files:**
- Modify: `src/AtomUI.City.State/StateCollectionChange.cs`
- Modify: `docs/modules/state/implementation-plan.md`
- Test: `tests/AtomUI.City.State.Tests/StateCollectionTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void CollectionChangeRejectsNegativeCollectionVersionInit()
{
    var change = new StateCollectionChange<string, int>(
        StateCollectionChangeKind.Added,
        "settings",
        HasOldItem: false,
        OldItem: default,
        HasNewItem: true,
        NewItem: 1,
        CollectionVersion: 1,
        ItemVersion: 1);

    Assert.Throws<ArgumentOutOfRangeException>(() => change with { CollectionVersion = -1 });
}

[Fact]
public void CollectionChangeRejectsNegativeItemVersionInit()
{
    var change = new StateCollectionChange<string, int>(
        StateCollectionChangeKind.Added,
        "settings",
        HasOldItem: false,
        OldItem: default,
        HasNewItem: true,
        NewItem: 1,
        CollectionVersion: 1,
        ItemVersion: 1);

    Assert.Throws<ArgumentOutOfRangeException>(() => change with { ItemVersion = -1 });
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionChangeRejectsNegativeCollectionVersionInit|FullyQualifiedName~CollectionChangeRejectsNegativeItemVersionInit"`
Expected: FAIL because collection change `with` init currently accepts negative versions.

- [ ] **Step 3: Write minimal implementation**

Back `StateCollectionChange<TKey,TItem>.CollectionVersion` and `ItemVersion` with init setters that reject values below 0. Update the State implementation matrix to record this boundary-hardening batch.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj --filter "FullyQualifiedName~CollectionChangeRejectsNegativeCollectionVersionInit|FullyQualifiedName~CollectionChangeRejectsNegativeItemVersionInit"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `fix(State): validate collection change versions`
