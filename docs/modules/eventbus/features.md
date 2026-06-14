# AtomUI.City.EventBus Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | 公开合同 | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publish | Completed | IEventBus.PublishAsync, IEventBus.PostAsync, EventPublishResult | EventPublicationTests |
| AUC-EVENTBUS-002 | Subscription Lifecycle | Completed | IEventSubscription, EventSubscriptionOptions | EventSubscriptionTests |
| AUC-EVENTBUS-003 | Contract Registry | Completed | IEventContractRegistry, EventContractDescriptor | EventContractRegistryTests |
| AUC-EVENTBUS-004 | Dispatch Policy | Completed | EventDispatchPolicy, EventErrorPolicy, EventSubscriptionOptions | EventDispatchingTests |
| AUC-EVENTBUS-005 | Diagnostics | Completed | EventDiagnosticIds | EventDiagnosticsTests |
| AUC-EVENTBUS-006 | DI Registration | 准备开始产品实现 | EventBusServiceCollectionExtensions | EventBusRegistrationTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Publish 不隐式切 UI 线程。 | 必须有实现、测试或工程门禁证据。 |
| handler 外部代码不能在总线内部锁内执行。 | 必须有实现、测试或工程门禁证据。 |
| 订阅必须返回可释放句柄并绑定 owner。 | 必须有实现、测试或工程门禁证据。 |
| 跨插件事件类型必须来自 Host 共享 contract 程序集。 | 必须有实现、测试或工程门禁证据。 |
| 默认派发顺序稳定。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-EVENTBUS-001 Typed Publish

Feature ID: `AUC-EVENTBUS-001`
Status: Completed
Goal: 按事件类型发布并返回 handler 结果。
Public Contract: IEventBus.PublishAsync, EventPublishResult
Runtime / Build Behavior: 按事件类型发布并返回 handler 结果；PostAsync 返回 accepted/rejected 结果并使用同一 event id delivery；publish options 的 correlation、causation 和 depth 进入 handler context。
Failure Behavior: event 为 null、contract 非法、handler 失败、取消、disposed bus、非法 options 和非法 result 边界。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventPublicationTests`。
Required Assertions: 断言 delivery/post result 边界、null event、预取消 token、disposed bus、publish options 边界、result immutable/null delivery、error policy、diagnostics、correlation/causation propagation。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-002 Subscription Lifecycle

Feature ID: `AUC-EVENTBUS-002`
Status: Completed
Goal: 订阅 Active/Disposed、owner 释放和 bus dispose 清理。
Public Contract: IEventBus, IEventSubscription, EventSubscriptionOptions
Runtime / Build Behavior: 订阅 Active/Disposed、owner 释放、bus dispose 清理和 in-flight drain。
Failure Behavior: 重复释放、owner dispose、stopped owner subscribe、bus dispose、插件 unload、已 Disposed 后 stop with canceled token。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventSubscriptionTests`。
Required Assertions: 断言 dispose 后不再收到事件、StopAsync 移除新发布快照、等待 in-flight handler、owner stop/cancellation 释放、stopped owner 拒绝、bus dispose 清理 active subscriptions、已 Disposed 后 StopAsync 幂等。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-003 Contract Registry

Feature ID: `AUC-EVENTBUS-003`
Status: Completed
Goal: 登记事件 contract 和 plane。
Public Contract: IEventContractRegistry, EventContractDescriptor
Runtime / Build Behavior: 登记 shared event contract、保持 type/id 映射稳定，并为未登记内部事件创建稳定默认 shared descriptor。
Failure Behavior: 跨插件私有类型拒绝、default contract id 拒绝、重复 contract id 和重复 event type 拒绝。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventContractRegistryTests`。
Required Assertions: 断言 shared contract assembly match、重复 contract id、重复 descriptor、稳定默认映射、plugin-private descriptor default id 拒绝、shared registry 拒绝 plugin-private descriptor。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-004 Dispatch Policy

Feature ID: `AUC-EVENTBUS-004`
Status: Completed
Goal: 顺序派发和错误策略。
Public Contract: EventDispatchPolicy, EventErrorPolicy
Runtime / Build Behavior: 默认 Serialized 派发；Current、UiThread、Background、Serialized 和错误策略语义稳定。
Failure Behavior: handler 异常按 ContinueAndReport 聚合并继续、StopPublication 停止剩余 delivery、FailPublisher 传播异常；未知 error policy 拒绝。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventDispatchingTests`。
Required Assertions: 断言默认 Serialized、dispatch/error enum 稳定值、异常聚合、停止策略、FailPublisher 传播、未知 error policy 拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-005 Diagnostics

Feature ID: `AUC-EVENTBUS-005`
Status: Completed
Goal: 发布、拒绝、delivery 失败和订阅诊断。
Public Contract: EventDiagnosticIds
Runtime / Build Behavior: 发布、拒绝、delivery 失败、取消和订阅诊断写入稳定 code，并在 HostDiagnosticRecord.Context 中携带定位字段。
Failure Behavior: diagnostics collector 缺失不影响 publish。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventDiagnosticsTests`。
Required Assertions: 断言 EventBus.Event* 现有代码、failure/cancellation 诊断包含 contract id、event id 和 subscription id，并覆盖 posted FailPublisher 后台失败诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-006 DI Registration

Feature ID: `AUC-EVENTBUS-006`
Status: 准备开始产品实现
Goal: 注册总线、registry 和 DI 生命周期。
Public Contract: EventBusServiceCollectionExtensions
Runtime / Build Behavior: 注册总线、registry 和 DI provider dispose 释放 singleton bus。
Failure Behavior: 重复注册和 override 行为。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `EventBusRegistrationTests`。
Required Assertions: 断言默认服务、可替换 diagnostics 和 provider dispose 释放 EventBus singleton。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
