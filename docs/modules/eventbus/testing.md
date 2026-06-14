# AtomUI.City.EventBus Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Publish 不隐式切 UI 线程。 | 至少一个明确测试断言，不能只断言流程成功。 |
| handler 外部代码不能在总线内部锁内执行。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 订阅必须返回可释放句柄并绑定 owner。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 跨插件事件类型必须来自 Host 共享 contract 程序集。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 默认派发顺序稳定。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Unit | EventPublicationTests | 断言 delivery/post result 边界、null event、预取消 token、disposed bus、publish options 边界、result immutable/null delivery、error policy、diagnostics、correlation/causation propagation。 | contract 非法、handler 失败、取消、disposed bus、非法 options、非法 result。 | Completed |
| AUC-EVENTBUS-002 | RuntimeLifecycle | EventSubscriptionTests | 断言 dispose 后不再收到事件、StopAsync 移除新发布快照、等待 in-flight handler、owner stop/cancellation 释放、stopped owner 拒绝、bus dispose 清理 active subscriptions、已 Disposed 后 StopAsync 幂等。 | 重复释放、owner dispose、stopped owner subscribe、bus dispose、插件 unload。 | Completed |
| AUC-EVENTBUS-003 | Unit | EventContractRegistryTests | 断言 shared contract assembly match、重复 contract id、重复 descriptor、稳定默认映射、plugin-private descriptor default id 拒绝、shared registry 拒绝 plugin-private descriptor。 | 跨插件私有类型拒绝、default contract id、重复 type/id 映射。 | Completed |
| AUC-EVENTBUS-004 | Unit | EventDispatchingTests | 断言默认 Serialized、dispatch/error enum 稳定值、异常聚合、停止策略、FailPublisher 传播、未知 error policy 拒绝。 | handler 异常、取消、继续或停止、未知 error policy。 | Completed |
| AUC-EVENTBUS-005 | Unit | EventDiagnosticsTests | 断言 EventBus.Event* 现有代码、failure/cancellation 诊断包含 contract id、event id 和 subscription id，并覆盖 posted FailPublisher 后台失败诊断。 | diagnostics collector 缺失不影响 publish。 | Completed |
| AUC-EVENTBUS-006 | Unit | EventBusRegistrationTests | 断言默认 services、默认 diagnostics、可替换 diagnostics 和 provider dispose 释放 EventBus singleton。 | 重复注册、override 行为、DI lifecycle。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
