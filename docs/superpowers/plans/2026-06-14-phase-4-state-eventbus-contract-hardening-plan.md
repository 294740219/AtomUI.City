# Phase 4 State 和 EventBus 合同硬化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 Phase 4 第一批产品级合同硬化，让 State 和 EventBus 在非法输入、取消、快照版本和调度选项边界上有稳定失败行为与测试证据。

**Architecture:** 本切片只修改 `AtomUI.City.State` 与 `AtomUI.City.EventBus` 的现有 public contract 边界，不引入持久化、递归发布检测、复杂背压或插件程序集验证。State 负责拒绝未知 enum、非法 schema/version；EventBus 负责在发布前观察取消、拒绝 null event，并拒绝未知 error policy。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [ ] 写 State 失败测试：`StateDefinition.Create` 拒绝未知 `StateLifetime`、`StateAccessPolicy`、`StateSnapshotPolicy` 和小于 1 的 schema version。
- [ ] 实现 StateDefinition 边界校验。
- [ ] 写 State snapshot 失败测试：`StateSnapshotEntry` 拒绝负 version 和小于 1 的 schema version；`StateSnapshot` 拒绝 entries 中的 null 项。
- [ ] 实现 StateSnapshot/StateSnapshotEntry 边界校验。
- [ ] 写 EventBus 失败测试：`PublishAsync` / `PostAsync` 拒绝 null event；`PublishAsync` 在无订阅者但 token 已取消时抛 `OperationCanceledException`。
- [ ] 实现 EventBus 发布入口校验和取消观察。
- [ ] 写 EventBus options 失败测试：`EventSubscriptionOptions.WithErrorPolicy` 拒绝未知 enum。
- [ ] 实现 EventSubscriptionOptions error policy 校验。
- [ ] 同步 `docs/modules/state/api-contracts.md`、`docs/modules/state/features.md`、`docs/modules/state/implementation-plan.md`、`docs/modules/eventbus/api-contracts.md`、`docs/modules/eventbus/features.md`、`docs/modules/eventbus/implementation-plan.md`，保持中文描述。
- [ ] 运行完整门禁：`dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj`、`dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj`、`dotnet build AtomUICity.slnx`、`dotnet test AtomUICity.slnx --no-build`、`bash engineering/check-docs.sh`、`bash engineering/check-public-api.sh`、`git diff --check`。
