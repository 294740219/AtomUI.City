# AtomUI.City.Core Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | 公开合同 | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-CORE-001 | Application Host Builder | Verified | ApplicationHostBuilder, ApplicationHostOptions, IApplicationHostBuilder, IApplicationContext | ApplicationHostBuilderTests; ApplicationHostRuntimeTests; ApplicationHostIndustrialLifecycleTests |
| AUC-CORE-002 | Lifecycle Pipeline | Verified | LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage | LifecycleMiddlewarePipelineTests; ApplicationHostIndustrialLifecycleTests |
| AUC-CORE-003 | Lifecycle Scope Tree | Verified | LifecycleScope, LifecycleScopeKind, LifecycleScopeState | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | Verified | ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext | ModuleBaseTests; ModuleDescriptorTests; ApplicationHostModuleLifecycleTests; ApplicationHostIndustrialLifecycleTests |
| AUC-CORE-005 | DI Registration Markers | Verified | ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, 标记接口 | ServiceRegistrationAttributeTests |
| AUC-CORE-006 | Host Diagnostics | Verified | IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds | HostDiagnosticsTests |
| AUC-CORE-007 | UI Dispatcher Contract | Verified | IUiDispatcher, UnavailableUiDispatcher | UiDispatcherIntegrationTests |
| AUC-CORE-008 | Generated Module Catalog | Verified | ApplicationModuleAttribute, GeneratedModuleManifestAttribute, IModuleRegistrar, IModuleRegistrarContext | GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests; NativeAOT headless publish/run |

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
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-CORE-001 Application Host Builder

Feature ID: `AUC-CORE-001`
Status: Verified
Goal: 构建 Host、冻结注册入口、创建不可变应用实例描述符。
Public Contract: ApplicationHostBuilder, ApplicationHostOptions, IApplicationHostBuilder, IApplicationContext
Runtime / Build Behavior: 验证应用身份、版本和路径，构建 Host，冻结注册入口，并在 Build 阶段一次性创建 IApplicationContext。
Failure Behavior: Build 后继续注册失败；重复 Build 行为明确；缺失 ApplicationId、无法解析版本或非法 ApplicationName/路径时 Build 失败并写 AUCHOST101。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ApplicationHostBuilderTests; ApplicationHostRuntimeTests`。
Required Assertions: 断言 Build 后 services/configuration 冻结、HostBuilt 诊断、根 scope 创建、Context 不可变且 Host Dispose 后仍可读取。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-002 Lifecycle Pipeline

Feature ID: `AUC-CORE-002`
Status: Verified
Goal: 按阶段执行 middleware，支持取消、异常聚合和幂等停止。
Public Contract: LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage
Runtime / Build Behavior: 按阶段执行 middleware，支持取消、异常聚合和幂等停止；一次 Host transaction 的嵌套 stage 和 rollback 传播同一 operationId。
Failure Behavior: middleware 异常进入 Faulted 并写 AUCHOST108；取消不跳过 cleanup；重复 Stop 幂等；Stopped 后再次 Start 抛 InvalidOperationException。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: AUCHOST108 包含 stage、middlewareType、operationId、exceptionType；原始异常传播和 diagnostics sink 故障隔离必须稳定。
Tests: `LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests`。
Required Assertions: 断言 stage 顺序、同 stage 顺序、准确 middleware 归因、operationId 传播、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-003 Lifecycle Scope Tree

Feature ID: `AUC-CORE-003`
Status: Verified
Goal: 表达 application/module/plugin contribution/operation 所有权树。
Public Contract: LifecycleScope, LifecycleScopeKind, LifecycleScopeState
Runtime / Build Behavior: 表达 application/module/plugin contribution/operation 所有权树。
Failure Behavior: parent disposed 后禁止创建 child；child 释放失败诊断；重复释放幂等。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `LifecycleScopeTreeTests`。
Required Assertions: 断言 leaf-first、parent-child 状态、dispose 后 mutating API 失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-004 Module Contract

Feature ID: `AUC-CORE-004`
Status: Verified
Goal: 模块声明、依赖、配置、服务注册和初始化钩子。
Public Contract: ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext
Runtime / Build Behavior: 模块声明、依赖、PreConfigure 配置、服务注册阶段边界和初始化钩子。
Failure Behavior: 非模块类型、循环依赖、缺失依赖、重复 id、非法 origin、插件 descriptor 缺失 plugin id、服务配置阶段后继续 mutation 失败；默认 id 使用类型全名。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests`。
Required Assertions: 断言依赖排序、默认 id、显式 id、模块来源、PreConfigure 顺序、配置阶段禁止解析运行时服务、配置阶段结束后拒绝继续修改服务注册。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-005 DI Registration Markers

Feature ID: `AUC-CORE-005`
Status: Verified
Goal: 声明服务 lifetime 和暴露服务。
Public Contract: ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, 标记接口
Runtime / Build Behavior: 声明服务 lifetime 和暴露服务。
Failure Behavior: 冲突 lifetime 或服务类型无效必须失败或诊断。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ServiceRegistrationAttributeTests`。
Required Assertions: 断言 lifetime、exposed services、AOT metadata 可读。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-006 Host Diagnostics

Feature ID: `AUC-CORE-006`
Status: Verified
Goal: 记录 Host 构建、启动、停止和失败上下文。
Public Contract: IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds
Runtime / Build Behavior: 记录 Host 构建、启动、停止和失败上下文；Host lifecycle 摘要与 middleware 详细诊断通过 operationId 关联。
Failure Behavior: 诊断 collector 不可用不能中断 Host；记录必须可 snapshot。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `HostDiagnosticsTests`。
Required Assertions: 断言 AUCHOST001-003、AUCHOST101-108、失败诊断上下文及 operationId 关联。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-007 UI Dispatcher Contract

Feature ID: `AUC-CORE-007`
Status: Verified
Goal: 为 Presentation 提供调度抽象和不可用实现。
Public Contract: IUiDispatcher, UnavailableUiDispatcher
Runtime / Build Behavior: 为 Presentation 提供调度抽象和不可用实现。
Failure Behavior: 默认 dispatcher 不可用；Presentation 必须替换真实实现。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `UiDispatcherIntegrationTests`。
Required Assertions: 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CORE-008 Generated Module Catalog

Feature ID: `AUC-CORE-008`
Status: Verified
Goal: 在编译期生成 AOT 友好的模块描述、依赖和强类型工厂，并在 Host Build 时从启动根解析实际模块闭包。
Public Contract: ApplicationModuleAttribute, GeneratedModuleManifestAttribute, IModuleRegistrar, IModuleRegistrarContext
Runtime / Build Behavior: Generator 为可用模块生成 Catalog registrar；`ApplicationModuleAttribute` 声明默认根，`UseModule<TModule>()` 增加显式根；Host 合并并去重根模块，只实例化其 required/available optional 依赖闭包。生成 descriptor 优先于兼容反射 descriptor。
Failure Behavior: 多个默认应用根、非法模块工厂、重复 id、缺失 required 依赖、循环依赖、非法 registrar 或 registrar 注册冲突必须在编译期或 Build 阶段确定性失败。缺少生成清单时仅显式 `UseModule<TModule>()` 进入兼容路径。
Threading / Cancellation: Registrar 只在单线程 Build 配置阶段执行，不运行模块业务代码；Build 后 Catalog 和 roots 冻结。
Diagnostics: Generator 诊断使用 `AUCGEN*`；Build 失败写入 Host build diagnostics，默认路径不得扫描程序集模块类型。
Tests: `GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests`；`win-x64` NativeAOT headless publish/run。
Required Assertions: 零 `UseModule` 自动根、显式根、重复根去重、仅加载依赖闭包、生成 factory、缺失依赖、循环依赖、无生成清单兼容路径和真实进程启停。
Acceptance Criteria: 默认路径不通过反射读取 Module/DependsOn metadata，不调用 Activator 创建模块；trim/AOT 可达性由强类型生成代码和发布测试证明。
