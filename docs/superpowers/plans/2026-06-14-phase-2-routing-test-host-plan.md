# Phase 2 Routing Test Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-006 Routing Test Host` 的产品级测试闭环，使 route graph、match、navigation helper、冲突失败、not-found 诊断和取消行为可稳定断言。

**Architecture:** `RoutingTestHostBuilder` 负责声明 route graph、冻结配置和冲突检测；`RoutingTestHost` 持有 immutable route snapshot 与 `TestDiagnostics`，提供 `Match` 和 `NavigateAsync` 两个测试入口。匹配逻辑仍是轻量测试实现，不启动真实 UI 或真实 routing runtime。

**Tech Stack:** .NET `net10.0` Debug 目标、`AtomUI.City.Testing` diagnostics、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围决策

包含：

- `RoutingTestHostBuilder` Build 后冻结 mutation entrypoint。
- duplicate route name 和 duplicate normalized pattern 在 Build 时失败。
- `RoutingTestHost.Diagnostics` 暴露 route test diagnostics。
- `Match` not found 写 `AUCTEST501`。
- `NavigateAsync(path, CancellationToken)` 观察取消并复用 match 结果。

不包含：

- 真实 navigation scope。
- route guard/resolver 完整模型。
- 插件 route contribution owner revoke。
- `AUC-TESTING-007` 及后续 Feature。

## 任务

### 任务 1：冻结 Routing Test Host 合同

- [ ] 更新 `docs/modules/testing/api-contracts.md`，补齐 `RoutingTestHostBuilder.MapRoute`、`Build`、`RoutingTestHost.Match`、`NavigateAsync`。
- [ ] 运行 `bash engineering/check-docs.sh`。
- [ ] 提交 `docs: freeze routing test host contracts`。

### 任务 2：实现 builder 冻结和 route conflict

- [ ] 在 `tests/AtomUI.City.Testing.Tests/RoutingTestHostTests.cs` 添加 `BuildFreezesRoutingTestHostBuilder`。
- [ ] 添加 `BuildRejectsDuplicateRouteNameAndPattern`。
- [ ] 运行 `dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "BuildFreezesRoutingTestHostBuilder|BuildRejectsDuplicateRouteNameAndPattern"`，预期失败。
- [ ] 在 `src/AtomUI.City.Testing/RoutingTestHostBuilder.cs` 增加冻结和 conflict 校验。
- [ ] 重新运行 focused tests，预期通过。

### 任务 3：实现 diagnostics 和 navigation helper

- [ ] 在 `RoutingTestHostTests.cs` 添加 `MatchRecordsDiagnosticsWhenRouteIsNotFound`。
- [ ] 添加 `NavigateAsyncObservesCancellationToken`。
- [ ] 运行 focused tests，预期编译或运行失败。
- [ ] 在 `RoutingTestHost.cs` 增加 `Diagnostics` 和 `NavigateAsync`，not found 写 `AUCTEST501`。
- [ ] 运行 `dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter RoutingTestHostTests`。

### 任务 4：更新状态并运行门禁

- [ ] 将 `AUC-TESTING-006` 标记为 `Implemented`、`Verified` 和 `None for Phase 2 slice`。
- [ ] 运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [ ] 提交 `feat: harden routing test host`。
