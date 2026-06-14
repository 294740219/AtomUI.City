# AtomUI.City.EventBus Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `EventBus.EventPublished` | EventPublished | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventAccepted` | EventAccepted | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventRejected` | EventRejected | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryFailed` | EventDeliveryFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryCancelled` | EventDeliveryCancelled | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionAdded` | EventSubscriptionAdded | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionDisposed` | EventSubscriptionDisposed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |

## 产品级必须诊断的失败

- 未登记跨边界 contract：拒绝 publish 并诊断。
- handler 抛异常：记录 contract id、event id、subscription id、eventType、handlerType、operationId。
- subscription dispose 中 handler 正在执行：等待完成或按 cancellation 策略结束。
- 重复 dispose：幂等。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，implementation plan 必须记录为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.EventBus.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
