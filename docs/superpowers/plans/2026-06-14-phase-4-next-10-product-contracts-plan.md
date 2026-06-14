# Phase 4 Next 10 Product Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete ten small product-contract hardening tasks for AtomUI.City.EventBus, committing each task independently.

**Architecture:** Keep the current in-memory EventBus architecture and tighten only public contract boundaries. Each task adds a focused failing test first, applies the smallest implementation change, updates the relevant Chinese product docs when the contract changes, runs focused verification, and commits.

**Tech Stack:** .NET `net10.0` Debug target, xUnit, `engineering/check-docs.sh`, `engineering/check-public-api.sh`, `git diff --check`.

---

## Tasks

### Task 1: EventContractId rejects surrounding whitespace

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`
- Modify: `src/AtomUI.City.EventBus/EventContractId.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that `new EventContractId(" atomui.city.event.v1 ")` throws `ArgumentException`.**
- [x] **Step 2: Run `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --filter EventContractRegistryTests` and verify the new test fails.**
- [x] **Step 3: Reject contract ids whose value is not equal to `value.Trim()`.**
- [x] **Step 4: Document the contract id whitespace boundary.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): reject padded contract ids`.**

### Task 2: EventContractId rejects control characters

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`
- Modify: `src/AtomUI.City.EventBus/EventContractId.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that contract ids containing newline characters throw `ArgumentException`.**
- [x] **Step 2: Run focused registry tests and verify the new test fails.**
- [x] **Step 3: Reject any `char.IsControl` character in contract ids.**
- [x] **Step 4: Document the control-character boundary.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): reject control characters in contract ids`.**

### Task 3: PublishAsync rejects negative publish depth

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that `PublishAsync` with `new EventPublishOptions { PublishDepth = -1 }` throws `ArgumentOutOfRangeException`.**
- [x] **Step 2: Run focused publication tests and verify the new test fails.**
- [x] **Step 3: Validate publish options before creating diagnostics or snapshots.**
- [x] **Step 4: Document the publish depth boundary.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): reject negative publish depth`.**

### Task 4: PostAsync rejects negative publish depth

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: this plan file

- [x] **Step 1: Add a failing test that `PostAsync` with `new EventPublishOptions { PublishDepth = -1 }` throws `ArgumentOutOfRangeException` and does not accept the event.**
- [x] **Step 2: Run focused publication tests and verify the new test fails.**
- [x] **Step 3: Apply the existing publish-options validation at the PostAsync acceptance boundary.**
- [x] **Step 4: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 5: Commit with `fix(EventBus): validate posted publish options`.**

### Task 5: EventPublishResult rejects null deliveries

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventPublicationTests.cs`
- Modify: `src/AtomUI.City.EventBus/EventPublishResult.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that `EventPublishResult` rejects a deliveries list containing null.**
- [x] **Step 2: Run focused publication tests and verify the new test fails.**
- [x] **Step 3: Validate delivery entries before copying the immutable delivery list.**
- [x] **Step 4: Document the result delivery-entry boundary.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): reject null publish deliveries`.**

### Task 6: Shared contract registry rejects plugin-private descriptors

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventContractRegistryTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventContractRegistry.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: `docs/modules/eventbus/features.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/testing.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that `InMemoryEventContractRegistry.Register(EventContractDescriptor.PluginPrivate<TEvent>(...))` throws `InvalidOperationException`.**
- [x] **Step 2: Run focused registry tests and verify the new test fails.**
- [x] **Step 3: Reject plugin-private descriptors in the shared in-memory registry.**
- [x] **Step 4: Update EventBus contract registry docs and status.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): reject private descriptors in shared registry`.**

### Task 7: StopAsync remains idempotent after disposal

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventSubscriptionTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: `docs/modules/eventbus/api-contracts.md`
- Modify: this plan file

- [x] **Step 1: Add a failing test that calling `StopAsync` on an already disposed subscription with a canceled token returns without throwing.**
- [x] **Step 2: Run focused subscription tests and verify the new test fails.**
- [x] **Step 3: Check disposed state before observing cancellation in `StopAsync`.**
- [x] **Step 4: Document the idempotent disposed-stop boundary.**
- [x] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [x] **Step 6: Commit with `fix(EventBus): keep stopped subscriptions idempotent`.**

### Task 8: Delivery failure diagnostics include stable event context

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: `docs/modules/eventbus/diagnostics.md`
- Modify: this plan file

- [ ] **Step 1: Add a failing test that failure diagnostics include the contract id, event id, and subscription id.**
- [ ] **Step 2: Run focused diagnostics tests and verify the new test fails.**
- [ ] **Step 3: Include those identifiers in EventDeliveryFailed messages.**
- [ ] **Step 4: Document the diagnostic context.**
- [ ] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [ ] **Step 6: Commit with `fix(EventBus): enrich failure diagnostics`.**

### Task 9: Delivery cancellation diagnostics include stable event context

**Files:**
- Modify: `tests/AtomUI.City.EventBus.Tests/EventDiagnosticsTests.cs`
- Modify: `src/AtomUI.City.EventBus/InMemoryEventBus.cs`
- Modify: `docs/modules/eventbus/diagnostics.md`
- Modify: this plan file

- [ ] **Step 1: Add a failing test that cancellation diagnostics include the contract id, event id, and subscription id.**
- [ ] **Step 2: Run focused diagnostics tests and verify the new test fails.**
- [ ] **Step 3: Include those identifiers in EventDeliveryCancelled messages.**
- [ ] **Step 4: Document the diagnostic context.**
- [ ] **Step 5: Run focused EventBus tests, docs check, public API check, and `git diff --check`.**
- [ ] **Step 6: Commit with `fix(EventBus): enrich cancellation diagnostics`.**

### Task 10: Sync EventBus productization status

**Files:**
- Modify: `docs/modules/eventbus/features.md`
- Modify: `docs/modules/eventbus/implementation-plan.md`
- Modify: `docs/modules/eventbus/testing.md`
- Modify: this plan file

- [ ] **Step 1: Review AUC-EVENTBUS-001 through AUC-EVENTBUS-006 statuses after the first nine tasks.**
- [ ] **Step 2: Mark newly covered product-contract slices as `产品化进行中` / `部分通过` without declaring full release readiness.**
- [ ] **Step 3: Run `dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj`, `dotnet build AtomUICity.slnx`, `dotnet test AtomUICity.slnx --no-build`, `bash engineering/check-docs.sh`, `bash engineering/check-public-api.sh`, and `git diff --check`.**
- [ ] **Step 4: Commit with `docs(EventBus): track product contract progress`.**
