# AtomUI.City.EventBus Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 诊断目录以 EventBus 1.0 设计合同为准，既有源码名称只能作为设计输入，不能作为 Feature 完成证据。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 1.0 稳定诊断目录

下列 code 是 EventBus Application Plane 1.0 的兼容性承诺。Message 可以优化，code 不得复用或静默改变语义。

| Current Code | Name | Source |
| --- | --- | --- |
| `EventBus.EventPublished` | EventPublished | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventAccepted` | EventAccepted | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventRejected` | EventRejected | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventContractRejected` | EventContractRejected | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventPayloadProjectionFailed` | EventPayloadProjectionFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryStarted` | EventDeliveryStarted | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryCompleted` | EventDeliveryCompleted | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryFailed` | EventDeliveryFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryCancelled` | EventDeliveryCancelled | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDeliveryTimedOut` | EventDeliveryTimedOut | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventDropped` | EventDropped | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventChannelBackpressure` | EventChannelBackpressure | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionAdded` | EventSubscriptionAdded | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionDisabled` | EventSubscriptionDisabled | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionQuiescing` | EventSubscriptionQuiescing | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionDisposed` | EventSubscriptionDisposed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventSubscriptionTerminationFailed` | EventSubscriptionTerminationFailed | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.PluginContributionActivated` | Plugin contribution 原子提交成功。 | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.PluginContributionRejected` | capability、contract、channel 或 lifecycle 拒绝；最小 context 为 pluginId、contractId、channel、requestedAccess。 | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.PluginContributionQuiescing` | lease 已关闭新操作入口并开始 drain。 | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.EventPluginDrainTimedOut` | plugin EventBus contribution 超过领域总 drain deadline；最小 context 为 pluginId、drainTimeoutMilliseconds、activeOperations、activeSubscriptions、pendingRegistrations。 | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |
| `EventBus.PluginContributionDisposed` | lease 终止并报告最终状态。 | `src/AtomUI.City.EventBus/EventDiagnosticIds.cs` |

## 1.0 必须诊断的失败

- 未登记跨边界 contract：拒绝 publish 并诊断。
- handler 抛异常：记录 contract id、event id、subscription id，并与 posted FailPublisher 后台失败诊断保持同一 delivery 定位字段。
- handler 取消：记录 contract id、event id、subscription id，并与 handler failure 诊断区分。
- channel 背压丢弃或合并已接受事件：记录 contract id、event id 和具体策略，不能静默丢弃。
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
- `channel`：强类型 channel 名称。
- `partitionHash`：Partitioned publication 稳定 key 的截断 SHA-256；非分区模式为 null，默认不得记录原始 key。
- `backpressurePolicy`：发生拒绝、丢弃或合并时实际生效的策略。
- `correlationId`、`causationId`、`publishDepth`：跨 publication 的因果链。
- `ownerScopeId`、`handlerTypeId`、`dispatchTarget`：订阅所有者和执行目标的 Host-owned 字符串快照。
- `queueWaitDurationMs`、`handlerDurationMs`、`deliveryResult`：排队、处理耗时与终止结果。

## 诊断输出边界

- EventBus 只生成诊断事实，不直接访问文件系统。
- 内存、滚动文件、远程遥测和诊断包导出由 Core `IHostDiagnostics` 及其 sink 负责。
- diagnostic sink 的任何异常不得改变 Publish、Post、Delivery、Subscribe、Stop 或 Dispose 的业务结果。
- `AddEventBus` 提供的默认内存 sink 必须有界；应用预注册的 `IHostDiagnostics` 仍具有 override 权。
- 高频 Trace 可配置采样；拒绝、丢弃、失败、取消、超时和清理失败不采样。

## Payload 安全投影

- 默认不记录 payload，不调用 payload `ToString()`。
- 只有显式注册且显式启用的 projector 才可以生成摘要。
- projector 输出必须是容量受限的字符串快照，不得包含 payload、handler、`Type`、`Exception` 或插件对象。
- projector 失败只产生 `EventPayloadProjectionFailed`，不得拒绝或失败事件发布。
- Application Plane projector 不可被插件用来夺取 Root DI 所有权；插件 projector、PluginId 和 drain 诊断由 `AUC-EVENTBUS-009` 建立 lease 后落地。

## 诊断缺口处理

- 如果设计目标没有对应诊断码，必须在 `AUC-EVENTBUS-005` 仍为 `In Design` 时补齐，不能留给源码施工自行决定。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`EventDiagnosticsTests` 必须断言最终 1.0 诊断目录中的 code、severity 和最小 context；设计基线中的名称只有进入最终目录后才形成兼容性承诺。
