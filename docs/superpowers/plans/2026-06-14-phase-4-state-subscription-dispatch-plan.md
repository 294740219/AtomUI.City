# Phase 4 State Subscription Dispatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-STATE-004 State Subscription` 中 Background 调度的产品级行为，使后台订阅不阻塞状态提交，同时 handler 失败仍进入 diagnostics。

**Architecture:** `WritableState<T>` 继续在提交后、锁外通知订阅者；`StateSubscription` 按 dispatch policy 分流。`Background` policy 改为 fire-and-report 后台投递，`Queued` 保持现有队列语义，`Immediate` 和 `Dispatcher` 保持同步语义。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`IHostDiagnostics`、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [x] 写失败测试：Background 订阅 handler 阻塞时，`SetValue` 必须先返回。
- [x] 写诊断测试：Background 订阅 handler 抛异常时仍写入 `AUCSTA004`。
- [x] 实现 `StateSubscription` 的后台异步投递和失败诊断。
- [x] 调整既有 Background 测试，使用确定性等待而不是依赖同步完成。
- [x] 同步 `docs/modules/state/api-contracts.md`、`docs/modules/state/features.md`、`docs/modules/state/implementation-plan.md`、`docs/modules/state/threading-and-dispatch.md`。
- [ ] 运行完整门禁：`dotnet test tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj`、`dotnet build AtomUICity.slnx`、`dotnet test AtomUICity.slnx --no-build`、`bash engineering/check-docs.sh`、`bash engineering/check-public-api.sh`、`git diff --check`。
