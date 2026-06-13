# AtomUI.City.Core Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-CORE-001 | Application Host Builder | Ready to Start Product Implementation | ApplicationHostBuilder, ApplicationHostOptions, IApplicationHostBuilder | ApplicationHostBuilderTests; ApplicationHostRuntimeTests |
| AUC-CORE-002 | Lifecycle Pipeline | Ready to Start Product Implementation | LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests |
| AUC-CORE-003 | Lifecycle Scope Tree | Ready to Start Product Implementation | LifecycleScope, LifecycleScopeKind, LifecycleScopeState | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | Ready to Start Product Implementation | ModuleBase, IModule, ModuleDescriptor, DependsOnAttribute | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests |
| AUC-CORE-005 | DI Registration Markers | Ready to Start Product Implementation | ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, marker interfaces | ServiceRegistrationAttributeTests |
| AUC-CORE-006 | Host Diagnostics | Ready to Start Product Implementation | IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds | HostDiagnosticsTests |
| AUC-CORE-007 | UI Dispatcher Contract | Ready to Start Product Implementation | IUiDispatcher, UnavailableUiDispatcher | UiDispatcherIntegrationTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。 | 必须有实现、测试或工程门禁证据。 |
| ApplicationHostBuilder Build 后必须冻结服务注册入口。 | 必须有实现、测试或工程门禁证据。 |
| LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。 | 必须有实现、测试或工程门禁证据。 |
| StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则，Stopped 后再次 Start 必须失败。 | 必须有实现、测试或工程门禁证据。 |
| 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。 | 必须有实现、测试或工程门禁证据。 |
| IUiDispatcher 只定义抽象，Core 不提交真实 UI work。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [implementation-plan.md](implementation-plan.md) 追踪。

## AUC-CORE-001 Application Host Builder

Feature ID: `AUC-CORE-001`
Status: Ready to Start Product Implementation
Goal: 构建 Host、冻结注册入口、创建 ApplicationContext。
Public Contract: ApplicationHostBuilder, ApplicationHostOptions, IApplicationHostBuilder
Runtime / Build Behavior: 构建 Host、冻结注册入口、创建 ApplicationContext。
Failure Behavior: Build 后继续注册失败；重复 Build 行为明确；无效 options 抛异常。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `ApplicationHostBuilderTests; ApplicationHostRuntimeTests`。
Required Assertions: 断言 Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-002 Lifecycle Pipeline

Feature ID: `AUC-CORE-002`
Status: Ready to Start Product Implementation
Goal: 按阶段执行 middleware，支持取消、异常聚合和幂等停止。
Public Contract: LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage
Runtime / Build Behavior: 按阶段执行 middleware，支持取消、异常聚合和幂等停止。
Failure Behavior: middleware 异常进入 Faulted；取消不跳过 cleanup；重复 Stop 幂等；Stopped 后再次 Start 抛 InvalidOperationException。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests`。
Required Assertions: 断言 stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-003 Lifecycle Scope Tree

Feature ID: `AUC-CORE-003`
Status: Ready to Start Product Implementation
Goal: 表达 application/module/plugin contribution/operation 所有权树。
Public Contract: LifecycleScope, LifecycleScopeKind, LifecycleScopeState
Runtime / Build Behavior: 表达 application/module/plugin contribution/operation 所有权树。
Failure Behavior: parent disposed 后禁止创建 child；child 释放失败诊断；重复释放幂等。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `LifecycleScopeTreeTests`。
Required Assertions: 断言 leaf-first、parent-child 状态、dispose 后 mutating API 失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-004 Module Contract

Feature ID: `AUC-CORE-004`
Status: Ready to Start Product Implementation
Goal: 模块声明、依赖、配置、服务注册和初始化钩子。
Public Contract: ModuleBase, IModule, ModuleDescriptor, DependsOnAttribute
Runtime / Build Behavior: 模块声明、依赖、配置、服务注册和初始化钩子。
Failure Behavior: 循环依赖、缺失依赖、重复 id 失败；默认 id 使用类型全名。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests`。
Required Assertions: 断言依赖排序、默认 id、显式 id、配置阶段禁止解析运行时服务。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-005 DI Registration Markers

Feature ID: `AUC-CORE-005`
Status: Ready to Start Product Implementation
Goal: 声明服务 lifetime 和暴露服务。
Public Contract: ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, marker interfaces
Runtime / Build Behavior: 声明服务 lifetime 和暴露服务。
Failure Behavior: 冲突 lifetime 或服务类型无效必须失败或诊断。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `ServiceRegistrationAttributeTests`。
Required Assertions: 断言 lifetime、exposed services、AOT metadata 可读。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-006 Host Diagnostics

Feature ID: `AUC-CORE-006`
Status: Ready to Start Product Implementation
Goal: 记录 Host 构建、启动、停止和失败上下文。
Public Contract: IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds
Runtime / Build Behavior: 记录 Host 构建、启动、停止和失败上下文。
Failure Behavior: 诊断 collector 不可用不能中断 Host；记录必须可 snapshot。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `HostDiagnosticsTests`。
Required Assertions: 断言现有 AUCHOST001/002/003 和目标失败诊断上下文。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-007 UI Dispatcher Contract

Feature ID: `AUC-CORE-007`
Status: Ready to Start Product Implementation
Goal: 为 Presentation 提供调度抽象和不可用实现。
Public Contract: IUiDispatcher, UnavailableUiDispatcher
Runtime / Build Behavior: 为 Presentation 提供调度抽象和不可用实现。
Failure Behavior: 默认 dispatcher 不可用；Presentation 必须替换真实实现。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 implementation plan 中追踪。
Tests: `UiDispatcherIntegrationTests`。
Required Assertions: 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
