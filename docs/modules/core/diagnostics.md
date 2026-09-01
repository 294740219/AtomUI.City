# AtomUI.City.Core Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCHOST001` | HostBuilt | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST002` | HostStarted | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST003` | HostStopped | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST101` | HostBuildFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST102` | HostStartFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST103` | HostStopFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST104` | LifecycleScopeCleanupFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST105` | ModuleGraphFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST106` | ModuleLifecycleFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST107` | DispatcherUnavailable | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |
| `AUCHOST108` | LifecycleMiddlewareFailed | `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs` |

## 产品级必须诊断的失败

- 模块依赖循环：Build 失败，诊断包含 cycle path。
- 重复 module id：Build 失败，诊断包含已有 module 和重复 module。
- lifecycle middleware 抛异常：当前 stage 失败，Host 进入 Faulted 并执行清理。
- Build 后继续注册服务：抛 InvalidOperationException 或返回失败 Result。
- UnavailableUiDispatcher 被执行：返回调度失败，不触碰 UI。

`AUCHOST108` 的 `Stage` 必须是失败时的 lifecycle stage，context 固定包含 `middlewareType`、`operationId` 和 `exceptionType`。对应 `AUCHOST102` 或 `AUCHOST103` Host 摘要记录使用同一个 `operationId`；启动失败后的 rollback 也沿用启动事务 id。正常 cancellation 不产生 `AUCHOST108`。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Core.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。

## 容量边界

默认 Host 使用 `ApplicationHostOptions.DiagnosticsCapacity` 创建有界 InMemoryHostDiagnostics，默认容量 1024。达到容量后丢弃最旧记录、保留最新记录，并通过 `DroppedCount` 暴露累计丢弃数。Build 失败诊断可在 builder 上通过 `GetBuildDiagnostics()` 读取。
