# AtomUI.City.Testing Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-TESTING-001 | Test Host | Implemented | TestHost, TestHostBuilder | TestHostTests |
| AUC-TESTING-002 | Fake Dispatcher | Implemented | FakeUiDispatcher, FakeUiWorkItem | FakeUiDispatcherTests |
| AUC-TESTING-003 | Deterministic Scheduler | Implemented | DeterministicScheduler | SharedTestUtilitiesTests |
| AUC-TESTING-004 | Module Test Host | Ready to Start Product Implementation | ModuleTestHost, ModuleTestHostBuilder | ModuleTestHostTests |
| AUC-TESTING-005 | Plugin Test Host | Ready to Start Product Implementation | PluginTestHost, PluginTestHostBuilder | PluginTestHostTests |
| AUC-TESTING-006 | Routing Test Host | Ready to Start Product Implementation | RoutingTestHost, RoutingTestHostBuilder | RoutingTestHostTests |
| AUC-TESTING-007 | Source Generation Kit | Ready to Start Product Implementation | SourceGenerationTestCase, GeneratedSourceSnapshot | SourceGenerationTestKitTests |
| AUC-TESTING-008 | AOT Check | Ready to Start Product Implementation | AotCompatibilityCheck, AotCompatibilityDiagnostic | AotCompatibilityCheckTests |
| AUC-TESTING-009 | Test Layers | Ready to Start Product Implementation | TestLayerAttribute, TestLayerNames | TestLayerTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 生产项目不得引用 Testing。 | 必须有实现、测试或工程门禁证据。 |
| 测试不得依赖固定 `Task.Delay`。 | 必须有实现、测试或工程门禁证据。 |
| 释放、取消、unload、dispatcher 和 generated output 必须有断言。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [implementation-plan.md](implementation-plan.md) 追踪。

## AUC-TESTING-001 Test Host

Feature ID: `AUC-TESTING-001`
Status: Implemented
Goal: 提供通用 host、service 和 diagnostics 测试工具。
Public Contract: TestHost, TestHostBuilder
Runtime / Build Behavior: 构建 services、diagnostics、records 和 disposable tracker。
Failure Behavior: 配置非法、Dispose 后使用、未释放资源必须失败。
Threading / Cancellation: host dispose 可异步；测试不得依赖固定 delay。
Diagnostics: 失败上下文必须包含 test host record。
Tests: `TestHostTests`
Required Assertions: 断言 service、diagnostics、dispose、records。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-002 Fake Dispatcher

Feature ID: `AUC-TESTING-002`
Status: Implemented
Goal: 提供可手动推进的 UI dispatcher。
Public Contract: FakeUiDispatcher, FakeUiWorkItem
Runtime / Build Behavior: 排队、运行、取消和记录 UI work item。
Failure Behavior: work exception、取消、未 drain 都可断言。
Threading / Cancellation: 测试手动 drain，不使用 Task.Delay。
Diagnostics: diagnostic 必须包含 work item id 和 thread marker。
Tests: `FakeUiDispatcherTests`
Required Assertions: 断言 queue、UI 线程识别、异常、pending count。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-003 Deterministic Scheduler

Feature ID: `AUC-TESTING-003`
Status: Implemented
Goal: 提供虚拟时间和异步任务推进。
Public Contract: DeterministicScheduler
Runtime / Build Behavior: 按虚拟时间执行 delayed work，记录任务顺序。
Failure Behavior: 负 duration、任务异常、取消任务稳定失败或诊断。
Threading / Cancellation: 单线程手动推进。
Diagnostics: diagnostic 必须包含 scheduled time 和 task id。
Tests: `SharedTestUtilitiesTests`
Required Assertions: 断言虚拟时间推进、任务顺序、异常记录。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-004 Module Test Host

Feature ID: `AUC-TESTING-004`
Status: Ready to Start Product Implementation
Goal: 测试模块依赖图和生命周期。
Public Contract: ModuleTestHost, ModuleTestHostBuilder
Runtime / Build Behavior: 注册模块、执行 lifecycle、捕获 diagnostics。
Failure Behavior: 循环依赖、初始化失败、shutdown 失败可断言。
Threading / Cancellation: 生命周期 API 观察 token。
Diagnostics: diagnostic 必须包含 module type 和 stage。
Tests: `ModuleTestHostTests`
Required Assertions: 断言 module graph、lifecycle、diagnostics。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-005 Plugin Test Host

Feature ID: `AUC-TESTING-005`
Status: Ready to Start Product Implementation
Goal: 测试插件安装、加载、卸载和贡献撤销。
Public Contract: PluginTestHost, PluginTestHostBuilder
Runtime / Build Behavior: 加载测试插件包，跟踪 state、contribution 和 owner revoke。
Failure Behavior: manifest 错误、unload 后泄漏、依赖冲突可断言。
Threading / Cancellation: load/unload 观察 token。
Diagnostics: diagnostic 必须包含 plugin id 和 state。
Tests: `PluginTestHostTests`
Required Assertions: 断言 load/unload、contribution、owner revoke。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-006 Routing Test Host

Feature ID: `AUC-TESTING-006`
Status: Ready to Start Product Implementation
Goal: 测试 route graph、match 和 navigation。
Public Contract: RoutingTestHost, RoutingTestHostBuilder
Runtime / Build Behavior: 构造 route definitions，执行 match/navigation helper。
Failure Behavior: route 冲突、匹配失败、guard deny 可断言。
Threading / Cancellation: navigation token 可传入。
Diagnostics: diagnostic 必须包含 route id 和 pattern。
Tests: `RoutingTestHostTests`
Required Assertions: 断言 route build、match、navigation helper。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-007 Source Generation Kit

Feature ID: `AUC-TESTING-007`
Status: Ready to Start Product Implementation
Goal: 测试 generator 输出和诊断。
Public Contract: SourceGenerationTestCase, GeneratedSourceSnapshot
Runtime / Build Behavior: 编译 source files、additional files、references，断言 generated source 和 diagnostics。
Failure Behavior: 编译失败、输出变化、diagnostic 缺失可断言。
Threading / Cancellation: runner 观察测试 token。
Diagnostics: diagnostic 必须包含 hint name 和 source location。
Tests: `SourceGenerationTestKitTests`
Required Assertions: 断言 generated source snapshot、diagnostics、references。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-008 AOT Check

Feature ID: `AUC-TESTING-008`
Status: Ready to Start Product Implementation
Goal: 提供 AOT/trimming 风险检查工具。
Public Contract: AotCompatibilityCheck, AotCompatibilityDiagnostic
Runtime / Build Behavior: 检查 runtime reflection scan、dynamic code、unbounded Activator 等禁止模式。
Failure Behavior: 禁止模式未识别或误报必须测试。
Threading / Cancellation: 文件/metadata 扫描可取消。
Diagnostics: diagnostic 必须包含 rule id 和 symbol。
Tests: `AotCompatibilityCheckTests`
Required Assertions: 断言反射扫描、dynamic code、trimming 风险诊断。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TESTING-009 Test Layers

Feature ID: `AUC-TESTING-009`
Status: Ready to Start Product Implementation
Goal: 强制测试分层和交付追踪。
Public Contract: TestLayerAttribute, TestLayerNames
Runtime / Build Behavior: 标记 Unit、Contract、Integration、PlatformIntegration、TemplateSmoke、Dogfood 等层。
Failure Behavior: 测试缺少 layer 或 layer 非法时门禁失败。
Threading / Cancellation: 纯元数据检查。
Diagnostics: diagnostic 必须包含 test type 和 expected layer。
Tests: `TestLayerTests`
Required Assertions: 断言 Unit/Contract/Integration/Platform/Dogfood 分层标记。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
