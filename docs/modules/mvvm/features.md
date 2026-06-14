# AtomUI.City.Mvvm Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-MVVM-001 | ViewModel Base and Notification | Ready to Start Product Implementation | ViewModelBase | ViewModelBaseTests |
| AUC-MVVM-002 | Activation and Deactivation | Ready to Start Product Implementation | IActivatable, ICanDeactivate, ActivationScope | ActivationScopeTests; DeactivationTests |
| AUC-MVVM-003 | Command Execution | Ready to Start Product Implementation | CommandFactory, OperationScope, OperationResult | CommandTests |
| AUC-MVVM-004 | Interaction Requests | Ready to Start Product Implementation | Interaction<TRequest, TResult>, InteractionContext<TRequest> | InteractionTests |
| AUC-MVVM-005 | Validation Model | Ready to Start Product Implementation | ValidationScope, ValidationMessage, ValidationStatus | ValidationScopeTests |
| AUC-MVVM-006 | Operation and Cancellation Scope | Ready to Start Product Implementation | OperationScope, CommandExecutionState | CommandTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。 | 必须有实现、测试或工程门禁证据。 |
| Interaction 只表达请求，展示和 handler 注册由 Presentation 承担。 | 必须有实现、测试或工程门禁证据。 |
| Command、Activation、Interaction、Validation 都必须有取消和失败结果语义。 | 必须有实现、测试或工程门禁证据。 |
| ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-MVVM-001 ViewModel Base and Notification

Feature ID: `AUC-MVVM-001`
Status: Ready to Start Product Implementation
Goal: 定义框架推荐 ViewModel 基类和属性变更语义。
Public Contract: ViewModelBase
Runtime / Build Behavior: 提供属性通知、释放入口和可观测状态；不引用 Avalonia visual。
Failure Behavior: Dispose 后 mutating API 必须失败或静默拒绝并保持文档一致。
Threading / Cancellation: 属性通知发生在调用线程；需要 UI marshal 的工作由 Presentation 处理。
Diagnostics: 重复 notification storm 必须可通过测试定位到 property name。
Tests: `ViewModelBaseTests`
Required Assertions: 断言 PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-002 Activation and Deactivation

Feature ID: `AUC-MVVM-002`
Status: Ready to Start Product Implementation
Goal: 统一 ViewModel 激活、停用、关闭确认和导航中断。
Public Contract: IActivatable, ICanDeactivate, IConfirmDeactivate, ActivationScope
Runtime / Build Behavior: ActivationScope 管理 Created、Activating、Active、Deactivating、Inactive、Disposed；CanDeactivate 在 Presentation 替换 View 前执行。
Failure Behavior: CanDeactivate 拒绝时返回 DeactivationResult，不抛业务异常；激活失败不得进入 Active。
Threading / Cancellation: 激活和停用 API 必须支持 CancellationToken；取消后不得注册新的资源。
Diagnostics: activation diagnostics 必须包含 ViewModel type、scope id 和 stage。
Tests: `ActivationScopeTests; DeactivationTests`
Required Assertions: 断言状态机、拒绝停用、取消、异常映射和资源释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-003 Command Execution

Feature ID: `AUC-MVVM-003`
Status: Ready to Start Product Implementation
Goal: 提供 .NET 风格命令执行、CanExecute、并发和错误结果。
Public Contract: CommandFactory, OperationScope, OperationResult, CommandExecutionState
Runtime / Build Behavior: Command 执行进入 OperationScope；同步和异步命令统一返回 OperationResult；CanExecute 变化可被 Presentation 绑定。
Failure Behavior: 执行异常映射为 Failed；并发执行按 command policy 串行或拒绝；失败不得吞掉诊断。
Threading / Cancellation: 异步命令必须观察 token；取消返回 Cancelled，不混用 Success。
Diagnostics: command diagnostics 必须包含 command name、owner ViewModel 和 operation id。
Tests: `CommandTests`
Required Assertions: 断言成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-004 Interaction Requests

Feature ID: `AUC-MVVM-004`
Status: Ready to Start Product Implementation
Goal: 让 ViewModel 声明 UI 交互请求而不依赖具体 UI。
Public Contract: Interaction<TRequest, TResult>, InteractionContext<TRequest>, InteractionResult<TResult>
Runtime / Build Behavior: Interaction 发布 request，由 Presentation 注册 handler；结果回到 ViewModel。
Failure Behavior: 无 handler 返回 Failed；handler 抛异常映射为 Failed；重复处理必须由 handler scope 控制。
Threading / Cancellation: Interaction 可以异步取消；取消后 handler result 不得提交。
Diagnostics: interaction diagnostics 必须包含 request type、handler type 和 scope id。
Tests: `InteractionTests`
Required Assertions: 断言有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-005 Validation Model

Feature ID: `AUC-MVVM-005`
Status: Ready to Start Product Implementation
Goal: 定义输入验证消息、状态聚合和 Presentation 可绑定合同。
Public Contract: ValidationScope, ValidationMessage, ValidationStatus
Runtime / Build Behavior: ValidationScope 聚合 field/global message；状态变化可由 Presentation 绑定为视觉状态。
Failure Behavior: 未知 field、重复 message、Dispose 后更新必须有稳定行为。
Threading / Cancellation: 验证可以在后台线程计算，最终状态提交必须遵守调用方调度策略。
Diagnostics: validation diagnostics 必须包含 field、severity 和 owner scope。
Tests: `ValidationScopeTests`
Required Assertions: 断言消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-MVVM-006 Operation and Cancellation Scope

Feature ID: `AUC-MVVM-006`
Status: Ready to Start Product Implementation
Goal: 为命令、激活、交互提供统一 operation 状态和取消边界。
Public Contract: OperationScope, CommandExecutionState
Runtime / Build Behavior: OperationScope 持有 operation id、状态、取消源和异常摘要；结束后只读。
Failure Behavior: 重复 Complete/Fail/Cancel 按首次终态生效；Dispose 后不能启动新操作。
Threading / Cancellation: 取消必须先标记状态再通知执行体，避免状态乱序。
Diagnostics: operation diagnostics 必须包含 operation id、status 和 elapsed。
Tests: `CommandTests`
Required Assertions: 断言状态转换、取消顺序、重复终态、耗时字段和资源释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
