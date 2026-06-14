# Phase 4 Computed State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-STATE-003 Computed State` 的产品级 lazy invalidation 切片，使计算状态在无订阅者时只标记失效，在读取或存在订阅者时才重新计算。

**Architecture:** `ComputedState<T>` 保留当前同步计算模型，不引入异步计算、source generator 依赖分析或复杂循环图检测。本切片新增 dirty 标记和订阅计数：依赖变化时无订阅者只标记 dirty；有订阅者时立即重算并通知；读取时如果 dirty 或上次计算失败后依赖变化，则重新计算。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`IHostDiagnostics`、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [ ] 写失败测试：无订阅者时依赖变化只标记失效，不立即重算；下一次读取才重算。
- [ ] 写失败测试：构造函数拒绝 dependencies 中的 null 项。
- [ ] 实现 dirty 标记、订阅计数和 dependency null 校验。
- [ ] 保持现有订阅场景：有订阅者时依赖变化立即重算并通知。
- [ ] 同步 `docs/modules/state/api-contracts.md`、`docs/modules/state/features.md`、`docs/modules/state/implementation-plan.md`、`docs/modules/state/computed-state.md`。
- [ ] 运行完整门禁：`dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj`、`dotnet build AtomUICity.slnx`、`dotnet test AtomUICity.slnx --no-build`、`bash engineering/check-docs.sh`、`bash engineering/check-public-api.sh`、`git diff --check`。
