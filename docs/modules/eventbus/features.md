# AtomUI.City.EventBus Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publish | Ready to Start Product Implementation | IEventBus.PublishAsync, EventPublishResult | EventPublicationTests |
| AUC-EVENTBUS-002 | Subscription Lifecycle | Ready to Start Product Implementation | IEventSubscription, EventSubscriptionOptions | EventSubscriptionTests |
| AUC-EVENTBUS-003 | Contract Registry | Ready to Start Product Implementation | IEventContractRegistry, EventContractDescriptor | EventContractRegistryTests |
| AUC-EVENTBUS-004 | Dispatch Policy | Ready to Start Product Implementation | EventDispatchPolicy, EventErrorPolicy | EventDispatchingTests |
| AUC-EVENTBUS-005 | Diagnostics | Ready to Start Product Implementation | EventDiagnosticIds | EventDiagnosticsTests |
| AUC-EVENTBUS-006 | DI Registration | Ready to Start Product Implementation | EventBusServiceCollectionExtensions | EventBusRegistrationTests |

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
- Feature 完成状态必须能从 [implementation-plan.md](implementation-plan.md) 追踪。

## AUC-EVENTBUS-001 Typed Publish

Feature ID: `AUC-EVENTBUS-001`
Status: Ready to Start Product Implementation
Goal: 按事件类型发布并返回 handler 结果。
Public Contract: IEventBus.PublishAsync, EventPublishResult
Runtime / Build Behavior: 按事件类型发布并返回 handler 结果。
Failure Behavior: contract 未登记、handler 失败、取消。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventPublicationTests`。
Required Assertions: 断言 delivery result、error policy、diagnostics。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-002 Subscription Lifecycle

Feature ID: `AUC-EVENTBUS-002`
Status: Ready to Start Product Implementation
Goal: 订阅 Active/Disposed 和 owner 释放。
Public Contract: IEventSubscription, EventSubscriptionOptions
Runtime / Build Behavior: 订阅 Active/Disposed 和 owner 释放。
Failure Behavior: 重复释放、owner dispose、插件 unload。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventSubscriptionTests`。
Required Assertions: 断言 dispose 后不再收到事件。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-003 Contract Registry

Feature ID: `AUC-EVENTBUS-003`
Status: Ready to Start Product Implementation
Goal: 登记事件 contract 和 plane。
Public Contract: IEventContractRegistry, EventContractDescriptor
Runtime / Build Behavior: 登记事件 contract 和 plane。
Failure Behavior: 跨插件私有类型拒绝。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventContractRegistryTests`。
Required Assertions: 断言 shared contract、private plugin type 拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-004 Dispatch Policy

Feature ID: `AUC-EVENTBUS-004`
Status: Ready to Start Product Implementation
Goal: 顺序派发和错误策略。
Public Contract: EventDispatchPolicy, EventErrorPolicy
Runtime / Build Behavior: 顺序派发和错误策略。
Failure Behavior: handler 异常、取消、继续或停止。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventDispatchingTests`。
Required Assertions: 断言顺序、异常聚合、停止策略。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-005 Diagnostics

Feature ID: `AUC-EVENTBUS-005`
Status: Ready to Start Product Implementation
Goal: 发布、拒绝、delivery 失败和订阅诊断。
Public Contract: EventDiagnosticIds
Runtime / Build Behavior: 发布、拒绝、delivery 失败和订阅诊断。
Failure Behavior: diagnostics collector 缺失不影响 publish。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventDiagnosticsTests`。
Required Assertions: 断言 EventBus.Event* 现有代码。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-EVENTBUS-006 DI Registration

Feature ID: `AUC-EVENTBUS-006`
Status: Ready to Start Product Implementation
Goal: 注册总线和 registry。
Public Contract: EventBusServiceCollectionExtensions
Runtime / Build Behavior: 注册总线和 registry。
Failure Behavior: 重复注册和 override 行为。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `EventBusRegistrationTests`。
Required Assertions: 断言默认服务和可替换 diagnostics。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
