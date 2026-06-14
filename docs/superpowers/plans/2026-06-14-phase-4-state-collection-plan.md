# Phase 4 State Collection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-STATE-006 Collection State` 的产品级合同硬化切片，使集合快照、变更记录和事件参数在构造边界拒绝非法输入，并保持现有集合更新、版本和通知语义稳定。

**Architecture:** 本切片不改变 `StateCollection<TKey, TItem>` 的同步提交、批量合并通知和快照恢复流程。新增校验集中在 public contract 载体：`StateCollectionSnapshot<TKey, TItem>`、`StateCollectionSnapshotEntry<TKey, TItem>`、`StateCollectionChange<TKey, TItem>` 和 `StateCollectionChangedEventArgs<TKey, TItem>`，让非法版本、null key、null 条目和未知 change kind 在进入运行时恢复或通知链路前失败。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`IHostDiagnostics`、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [ ] 写失败测试：`StateCollectionSnapshot<TKey,TItem>` 拒绝负 collection version、null entry。
- [ ] 写失败测试：`StateCollectionSnapshotEntry<TKey,TItem>` 拒绝 null key、负 item version。
- [ ] 写失败测试：`StateCollectionChange<TKey,TItem>` 拒绝未知 change kind、null key、负 collection version、负 item version。
- [ ] 写失败测试：`StateCollectionChangedEventArgs<TKey,TItem>` 拒绝 null change 条目，并继续拒绝空列表。
- [ ] 实现 snapshot、snapshot entry、change record、changed event args 的最小合同校验。
- [ ] 保持现有 `StateCollectionTests` 行为：Add/Update/Remove/Clear、Range、Snapshot、Restore、只读集合、事件通知顺序不回归。
- [ ] 同步 `docs/modules/state/api-contracts.md`、`docs/modules/state/features.md`、`docs/modules/state/implementation-plan.md`、`docs/modules/state/collection-state.md`。
- [ ] 运行完整门禁：`dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj`、`dotnet build AtomUICity.slnx`、`dotnet test AtomUICity.slnx --no-build`、`bash engineering/check-docs.sh`、`bash engineering/check-public-api.sh`、`git diff --check`。
