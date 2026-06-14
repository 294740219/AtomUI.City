# AtomUI.City.Presentation Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-PRESENTATION-001 | UI Dispatcher Bridge | 已实现并通过产品合同测试 | AvaloniaUiDispatcher, IUiDispatcher | AvaloniaUiDispatcherTests; PresentationPlatformIntegrationTests |
| AUC-PRESENTATION-002 | View Registry and Locator | 已实现并通过产品合同测试 | ViewRegistry, IViewLocator, ViewForAttribute | ViewLocatorTests |
| AUC-PRESENTATION-003 | View Factory and Binding | 已实现并通过产品合同测试 | ViewFactory, ViewBinder, BoundViewHandle | ViewBindingTests |
| AUC-PRESENTATION-004 | Route Outlet Commit | 已实现并通过产品合同测试 | IRouteOutlet, RouteOutlet, RouteOutletCommitResult | RouteOutletTests |
| AUC-PRESENTATION-005 | Visual Lifecycle Feedback | 已实现并通过产品合同测试 | VisualLifecycleHub, VisualLifecycleEvent | VisualFeedbackTests |
| AUC-PRESENTATION-006 | Interaction and Validation Bridge | Ready to Start Product Implementation | InteractionHandlerRegistry, ValidationVisualStateBinding | PresentationInteractionHandlerTests; ValidationVisualStateBindingTests |
| AUC-PRESENTATION-007 | Localization and Resource Bridge | Ready to Start Product Implementation | PresentationLocalizationBridge, PresentationResourceRegistry | PresentationLocalizationBridgeTests; PresentationResourceRegistryTests |
| AUC-PRESENTATION-008 | Plugin UI Unload Coordination | Ready to Start Product Implementation | ActivePluginViewRegistry, PresentationPluginUnloadCoordinator | ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Presentation 负责 ViewModel -> View、UI Dispatcher、Outlet 提交和 UI 运行时桥接。 | 必须有实现、测试或工程门禁证据。 |
| 所有 VisualTree 修改必须在 UI dispatcher 上执行。 | 必须有实现、测试或工程门禁证据。 |
| ViewLocator 默认使用 generated manifest 或显式注册，不依赖运行时程序集扫描作为唯一机制。 | 必须有实现、测试或工程门禁证据。 |
| 插件 View、resource dictionary、localized binding、interaction handler 必须绑定可撤销 owner。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-PRESENTATION-001 UI Dispatcher Bridge

Feature ID: `AUC-PRESENTATION-001`
Status: 已实现并通过产品合同测试
Goal: 把 Core 的 UI dispatcher 抽象桥接到 Avalonia UI 线程。
Public Contract: AvaloniaUiDispatcher, IUiDispatcher
Runtime / Build Behavior: 所有 VisualTree 修改、resource dictionary 修改和 UI-bound notification 都通过 dispatcher 提交。
Failure Behavior: 非 UI 线程直接提交会 marshal 到 dispatcher；dispatcher 不可用返回 `PresentationError.DispatcherUnavailable`；work exception 原样传播并记录诊断。
Threading / Cancellation: InvokeAsync 必须观察 token；取消后不得执行 UI work item。
Diagnostics: dispatcher diagnostics 包含 operation id、calling thread id、dispatcher thread id 和 target action。
Tests: `AvaloniaUiDispatcherTests; PresentationPlatformIntegrationTests`
Required Assertions: 断言 UI 线程识别、后台 marshal、取消、异常映射和平台不可用。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-002 View Registry and Locator

Feature ID: `AUC-PRESENTATION-002`
Status: 已实现并通过产品合同测试
Goal: 建立 ViewModel -> View 的 AOT 友好解析。
Public Contract: ViewRegistry, IViewLocator, ViewForAttribute, ViewLookupRequest, ViewRegistrationOptions
Runtime / Build Behavior: View 注册来自 generator manifest 或显式注册；lookup 按 ViewModel type、view key、route id 和 owner context 查找。
Failure Behavior: View 未注册、重复注册、插件 owner 已卸载必须返回失败，不 fallback 到反射扫描或 assignable type 扫描。
Threading / Cancellation: registry lookup 可并发读取；注册、显式覆盖和撤销串行。
Diagnostics: view lookup diagnostics 包含 ViewModel type、view type、route id、owner、plugin id 和 contribution id。
Tests: `ViewLocatorTests`
Required Assertions: 断言 manifest 注册、显式覆盖、重复拒绝、插件撤销和 O(1) lookup 路径。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-003 View Factory and Binding

Feature ID: `AUC-PRESENTATION-003`
Status: 已实现并通过产品合同测试
Goal: 创建 View、设置 DataContext、建立 ViewModel 和 visual 的绑定边界。
Public Contract: ViewFactory, ViewBinder, BoundViewHandle
Runtime / Build Behavior: ViewFactory 通过 UI dispatcher 创建已注册 View；ViewBinder 设置 DataContext 并发布 attach/detach lifecycle。
Failure Behavior: 构造失败不污染 outlet；binding 失败释放已创建 View。
Threading / Cancellation: View 创建和 DataContext 设置在 UI dispatcher；取消后不得返回 bound handle。
Diagnostics: factory/binding diagnostics 包含 view type、ViewModel type、view key、constructor parameters 和耗时。
Tests: `ViewBindingTests`
Required Assertions: 断言构造参数、DataContext、失败回滚、handle dispose 和 lifecycle event。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-004 Route Outlet Commit

Feature ID: `AUC-PRESENTATION-004`
Status: 已实现并通过产品合同测试
Goal: 把 Routing/MVVM 产生的 View 提交到桌面 UI 容器。
Public Contract: IRouteOutlet, RouteOutlet, RouteOutletCommitPlan, RouteOutletCommitResult
Runtime / Build Behavior: replace 和 clear 通过 dispatcher 提交；同一 outlet 的 commit 串行执行；重复提交当前 handle 为 no-op；replace 成功前先释放旧 handle，再设置新 content。
Failure Behavior: outlet mismatch、dispatcher 失败、旧 handle dispose 失败或非法 replace plan 都返回失败并保持旧 visual；被拒绝的新 handle 会释放，释放失败写入诊断。
Threading / Cancellation: commit 必须在 UI dispatcher 串行执行；取消发生在 dispatcher attach 前时不替换 content，并释放被拒绝 handle。
Diagnostics: outlet diagnostics 包含 outlet name、requested outlet、operation、current view type、new view type 和 error。
Tests: `RouteOutletTests`
Required Assertions: 断言成功替换、失败回滚、取消、重复 commit、旧 view dispose 和结果状态。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-005 Visual Lifecycle Feedback

Feature ID: `AUC-PRESENTATION-005`
Status: 已实现并通过产品合同测试
Goal: 把 VisualTree attach/detach/focus/visibility 变化反馈给 ViewModel、State 或 EventBus。
Public Contract: VisualLifecycleHub, VisualLifecycleEvent, UiStateFeedbackPolicy
Runtime / Build Behavior: Visual 变化统一发布为 lifecycle event；attach、detach、load、unload、focus 和 visibility 事件保留通知顺序；策略决定哪些 UI state feedback 可以进入 ViewModel。
Failure Behavior: 反馈 handler 失败被隔离并写入诊断，不阻断后续 handler，不破坏 VisualTree。
Threading / Cancellation: visual event 必须在 UI dispatcher 捕获；同步发布无 token，异步消费者由上层调度策略承接。
Diagnostics: visual feedback diagnostics 包含 event kind、view type、target ViewModel type 和 error。
Tests: `VisualFeedbackTests`
Required Assertions: 断言 attach/detach、focus、visibility、反馈顺序和 handler 失败隔离。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-006 Interaction and Validation Bridge

Feature ID: `AUC-PRESENTATION-006`
Status: Ready to Start Product Implementation
Goal: 把 MVVM Interaction/Validation 映射到 UI handler 和视觉状态。
Public Contract: InteractionHandlerRegistry, ValidationVisualStateBinding
Runtime / Build Behavior: Interaction handler 按 request type 和 owner 查找；validation binding 将 ValidationScope 映射到控件状态。
Failure Behavior: 无 handler、重复 handler、控件已释放必须返回失败或撤销 binding。
Threading / Cancellation: handler 可以异步；UI 展示必须在 dispatcher；取消后不提交结果。
Diagnostics: interaction/validation diagnostics 必须包含 request type、control id 和 handler owner。
Tests: `PresentationInteractionHandlerTests; ValidationVisualStateBindingTests`
Required Assertions: 断言 handler 注册撤销、无 handler、验证消息变化、控件释放和取消。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-007 Localization and Resource Bridge

Feature ID: `AUC-PRESENTATION-007`
Status: Ready to Start Product Implementation
Goal: 把 Localization culture 变化同步到 AtomUI/Avalonia 资源和文本绑定。
Public Contract: PresentationLocalizationBridge, PresentationResourceRegistry
Runtime / Build Behavior: culture change 批量刷新 text binding、flow direction 和 resource dictionary；资源绑定 owner 可撤销。
Failure Behavior: 语言包缺失、resource dictionary 加载失败、target 已释放必须诊断并继续刷新其他 target。
Threading / Cancellation: 刷新在 UI dispatcher 执行；后台语言包加载完成后再 marshal。
Diagnostics: localization bridge diagnostics 必须包含 culture、resource key 和 target type。
Tests: `PresentationLocalizationBridgeTests; PresentationResourceRegistryTests`
Required Assertions: 断言 culture 切换、fallback、resource revoke、插件资源卸载和局部失败隔离。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-PRESENTATION-008 Plugin UI Unload Coordination

Feature ID: `AUC-PRESENTATION-008`
Status: Ready to Start Product Implementation
Goal: 插件卸载时撤销 active view、资源、handler 和 localization binding。
Public Contract: ActivePluginViewRegistry, PresentationPluginUnloadCoordinator
Runtime / Build Behavior: 插件 unload 前枚举 active view；可关闭则 detach，不能关闭则阻止 unload 或返回协调失败。
Failure Behavior: active view 拒绝关闭、资源撤销失败、handler 仍被引用必须报告并避免半卸载。
Threading / Cancellation: unload coordination 串行执行；所有 UI 变更在 dispatcher。
Diagnostics: plugin UI diagnostics 必须包含 plugin id、active view count、failed contribution。
Tests: `ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests`
Required Assertions: 断言 active view lease、卸载撤销、拒绝卸载、资源释放和重复 unload。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
