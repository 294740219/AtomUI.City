# AtomUI.City.EventBus Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 诊断目录以 EventBus 1.0 设计合同为准，既有源码名称只能作为设计输入，不能作为 Feature 完成证据。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 设计基线诊断族

下列名称是需要在 `AUC-EVENTBUS-005` 收口中保留或迁移的设计基线，不代表完整 1.0 诊断目录，也不表示已经实现或验证。

| Current Code | Name | Source |
| --- | --- | --- |
| `EventBus.EventPublished` | EventPublished | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventAccepted` | EventAccepted | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventRejected` | EventRejected | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryFailed` | EventDeliveryFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryCancelled` | EventDeliveryCancelled | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionAdded` | EventSubscriptionAdded | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionQuiescing` | EventSubscriptionQuiescing | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionDisposed` | EventSubscriptionDisposed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionTerminationFailed` | EventSubscriptionTerminationFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |

## 1.0 必须诊断的失败

- 未登记跨边界 contract：拒绝 publish 并诊断。
- handler 抛异常：记录 contract id、event id、subscription id，并与 posted FailPublisher 后台失败诊断保持同一 delivery 定位字段。
- handler 取消：记录 contract id、event id、subscription id，并与 handler failure 诊断区分。
- subscription dispose 中 handler 正在执行：等待完成或按 cancellation 策略结束。
- subscription 终止或资源清理失败：记录 subscription id、event type、dispatch policy 和 error policy，并继续其余清理。
- 重复 dispose：幂等。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

EventBus 1.0 最小稳定 context key：

- `contractId`：事件 contract id。
- `eventId`：单次发布 id。
- `subscriptionId`：delivery 或订阅 id。
- `eventType`：事件类型全名。
- `dispatchPolicy`：订阅派发策略。
- `errorPolicy`：订阅错误策略。

## 诊断缺口处理

- 如果设计目标没有对应诊断码，必须在 `AUC-EVENTBUS-005` 仍为 `In Design` 时补齐，不能留给源码施工自行决定。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`EventDiagnosticsTests` 必须断言最终 1.0 诊断目录中的 code、severity 和最小 context；设计基线中的名称只有进入最终目录后才形成兼容性承诺。
