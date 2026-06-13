# AtomUI.City.Presentation Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- Presentation 负责 ViewModel -> View -> Outlet -> VisualTree。
- VisualTree 变化必须通过生命周期事件或绑定反馈回 ViewModel/State。
- View 创建和提交必须在 UI dispatcher 上执行。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Presentation` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-PRESENTATION-001 | UI Dispatcher | AvaloniaUiDispatcherTests |
| AUC-PRESENTATION-002 | View Locator | ViewLocatorTests |
| AUC-PRESENTATION-003 | View Binding | ViewBindingTests |
| AUC-PRESENTATION-004 | Route Outlet | RouteOutletTests |
| AUC-PRESENTATION-005 | Presentation Runtime | PresentationRuntimeTests |
| AUC-PRESENTATION-006 | Localization Bridge | PresentationLocalizationBridgeTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.PluginSystem 运行时直接依赖插件实现类型` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Presentation 诊断与测试设计

适用范围：Presentation 诊断字段、fake runtime、平台集成测试和测试矩阵

### 1. 诊断

必须记录：

- UI runtime ready / stopping。
- Dispatcher 投递失败。
- ViewLocator 命中和失败。
- View 创建耗时。
- Binding 耗时。
- Outlet commit 计划和结果。
- Activation visual adapter 执行。
- Interaction handler 执行。
- Resource contribution 和撤销。
- 插件 UI 关闭和资源清理。

诊断信息必须包含 ScopeId、WindowId、NavigationScopeId、RouteId、ViewModel type、View type、PluginId 和 ContributionId。

### 2. 测试工具

Testing 包应提供：

- FakePresentationRuntime。
- FakeUiDispatcher。
- TestViewLocator。
- TestViewFactory。
- TestRouteOutlet。
- TestPresentationCommitter。
- Interaction test handler。
- View binding recorder。
- Plugin presentation resource test host。

Presentation 测试应能在无真实 AtomUI/Avalonia UI 的环境中运行。真实 UI 集成测试单独放到平台集成测试中。

### 3. 模块测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| ViewLocator 成功 | Unit | ViewModel 定位到 View。 |
| ViewLocator 失败 | Unit | commit failed 并记录诊断。 |
| 多默认 View | Analyzer/Generator | 输出重复 View 诊断。 |
| View 创建失败 | Unit | commit failed，旧内容保留。 |
| Outlet commit 成功 | Unit | View attached 到 outlet。 |
| Outlet commit 失败回滚 | Unit | 新 View 释放，旧 content 保留。 |
| Interaction 状态 | Unit | Completed、Canceled、NotHandled、Failed 均可断言。 |
| ActivationScope 释放 | Unit | binding 和 UI 事件订阅被释放。 |
| 插件停用 | Unit | View 关闭，资源撤销。 |
| dispatcher stopped | Unit | 停止后拒绝投递。 |
| real dispatcher | Platform integration | 真实 UI dispatcher 可执行最小投递。 |
