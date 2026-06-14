# AtomUI.City.State Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | 公开合同 | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-STATE-001 | Writable State | Completed | IWritableState<T>, WritableState<T> | WritableStateTests |
| AUC-STATE-002 | Application State | Completed | IApplicationState, ApplicationStateRegistry, StateDefinition<T> | ApplicationStateTests; StateDefinitionTests |
| AUC-STATE-003 | Computed State | Completed | IComputedState<T>, ComputedState<T> | ComputedStateTests |
| AUC-STATE-004 | State Subscription | Completed | IStateSubscription, IStateReaction, StateSubscriptionOptions | StateScopeTests; StateThreadingTests |
| AUC-STATE-005 | State Snapshot | Completed | StateSnapshot, StateSnapshotEntry | StateSnapshotTests |
| AUC-STATE-006 | Collection State | Completed | StateCollection<TKey,TItem> | StateCollectionTests |
| AUC-STATE-007 | Diagnostics | Completed | StateDiagnosticIds | StateDiagnosticsTests |
| AUC-STATE-008 | Threading | 准备开始产品实现 | StateDispatchPolicy | StateThreadingTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 状态写入先完成原子提交，再通知订阅者。 | 必须有实现、测试或工程门禁证据。 |
| 默认不隐式切 UI 线程。 | 必须有实现、测试或工程门禁证据。 |
| StateSnapshot 创建后不可变。 | 必须有实现、测试或工程门禁证据。 |
| ComputedState 不能形成循环依赖。 | 必须有实现、测试或工程门禁证据。 |
| 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-STATE-001 Writable State

Feature ID: `AUC-STATE-001`
Status: Completed
Goal: 线程安全读写、变更通知和 access policy。
Public Contract: IWritableState<T>, WritableState<T>
Runtime / Build Behavior: 线程安全读写、变更通知、version 递增和 access policy。
Failure Behavior: updater 拒绝或失败、handler 失败、disposed state、ReadOnly 写拒绝。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `WritableStateTests`。
Required Assertions: 断言原子更新、version、提交后通知、相等值不通知、订阅 dispose、disposed mutation rejection、updater 异常诊断和写拒绝/access policy。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-002 Application State

Feature ID: `AUC-STATE-002`
Status: Completed
Goal: 通过 DI 访问应用级共享状态。
Public Contract: IApplicationState, ApplicationStateRegistry
Runtime / Build Behavior: 通过 DI 访问应用级共享状态。
Failure Behavior: 未注册、重复注册、写入拒绝、Update null updater、StateDefinition enum 和 schema version 边界。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ApplicationStateTests`。
Required Assertions: 断言注册、读取、writer、not registered、Update 参数边界、StateDefinition enum 和 schema version 边界。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-003 Computed State

Feature ID: `AUC-STATE-003`
Status: Completed
Goal: 依赖状态变更后重新计算。
Public Contract: IComputedState<T>, ComputedState<T>
Runtime / Build Behavior: 依赖状态变更后 lazy invalidation、缓存和按需重新计算。
Failure Behavior: compute 异常、dispose、null dependency、订阅阶段部分失败清理。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ComputedStateTests`。
Required Assertions: 断言 lazy invalidation、依赖失效、缓存、异常诊断、null dependency 拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-004 State Subscription

Feature ID: `AUC-STATE-004`
Status: Completed
Goal: 生命周期绑定订阅和释放。
Public Contract: IStateSubscription, IStateReaction
Runtime / Build Behavior: 生命周期绑定订阅和释放；延迟调度回调执行前重新检查 Dispose 状态。
Failure Behavior: 重复释放、owner dispose、callback 失败；Background 和 Dispatcher handler 失败进入 diagnostics。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `StateScopeTests; StateThreadingTests`。
Required Assertions: 断言 dispose 后不通知、Dispatcher pending callback 释放抑制、Background 不阻塞状态提交、Background handler 失败诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-005 State Snapshot

Feature ID: `AUC-STATE-005`
Status: Completed
Goal: 捕获和恢复状态条目。
Public Contract: StateSnapshot, StateSnapshotEntry
Runtime / Build Behavior: 捕获 Persisted 状态并恢复兼容状态条目。
Failure Behavior: 版本不兼容、policy 拒绝、restore 失败、entry 边界非法。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `StateSnapshotTests`。
Required Assertions: 断言不可变、过滤、restore diagnostics、policy 拒绝、entry version/schema 边界、entries 不含 null。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-006 Collection State

Feature ID: `AUC-STATE-006`
Status: Completed
Goal: 集合变更、快照、事件和集合生命周期。
Public Contract: IStateCollection<TKey,TItem>, StateCollection<TKey,TItem>
Runtime / Build Behavior: 集合变更、快照、事件和 dispose 生命周期。
Failure Behavior: 重复 key、missing key、clear、disposed collection；集合快照、事件参数和变更记录拒绝 null key、null 条目、未知 change kind 和负 version。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `StateCollectionTests`。
Required Assertions: 断言 change kind、item version、collection version、快照不可变、非法构造参数、dispose 幂等、dispose 后读 API 可用、mutation/restore/subscription API 拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-007 Diagnostics

Feature ID: `AUC-STATE-007`
Status: Completed
Goal: 稳定诊断码、关键失败路径诊断和定位上下文。
Public Contract: StateDiagnosticIds
Runtime / Build Behavior: AUCSTA001-010 诊断码稳定；失败记录包含 code、severity、message 和定位 context。
Failure Behavior: diagnostics collector 缺失时不得影响主流程；handler、update、restore、dispose 和 access failure 必须写入对应诊断。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `StateDiagnosticsTests`。
Required Assertions: 断言 AUCSTA001-010、唯一性、格式、severity 和定位 context。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-STATE-008 Threading

Feature ID: `AUC-STATE-008`
Status: 准备开始产品实现
Goal: 多线程更新和派发策略。
Public Contract: StateDispatchPolicy
Runtime / Build Behavior: 多线程更新和派发策略。
Failure Behavior: 并发写、调度器不可用。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `StateThreadingTests`。
Required Assertions: 断言不隐式 UI。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
