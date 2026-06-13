# AtomUI.City.Presentation Interaction And Validation 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Interaction And Validation` 相关实现决策，不重新定义模块边界。

## 设计决策

- Presentation 负责 ViewModel -> View -> Outlet -> VisualTree。
- VisualTree 变化必须通过生命周期事件或绑定反馈回 ViewModel/State。
- View 创建和提交必须在 UI dispatcher 上执行。

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

## AtomUI.City.Presentation Interaction 与 Validation 设计

适用范围：Interaction handler、Dialog/FilePicker/Toast、Validation visual state 和命令绑定

### 1. Interaction Handler

Presentation 负责把 MVVM Interaction Request 映射到 UI。

支持场景：

- 确认。
- 输入。
- 文件选择。
- Dialog。
- Toast / Notification。
- Window 选择。

规则：

- Handler 运行在 UI Thread。
- Handler 注册绑定 ActivationScope、WindowScope 或 ApplicationScope。
- ViewModel 停用时，未完成 Interaction 返回 Canceled。
- 插件停用时，插件 Interaction 返回 Canceled。
- Handler 缺失返回 NotHandled，并记录诊断。

Presentation 不把具体 Dialog 业务模型强加给应用。

### 2. Validation 集成

Mvvm 定义验证状态，Presentation 负责展示。

Presentation 需要支持：

- 读取 `ObservableValidator` 或框架验证状态。
- 把错误映射到 AtomUI/Avalonia validation visual state。
- Command 与验证状态变化后的 UI 刷新。
- 插件 View 的验证资源释放。

Validation failed 不是异常，不进入 fatal error。

### 3. Command Binding

Presentation 可以增强 Command Binding。

职责：

- 把 `IRelayCommand` / `IAsyncRelayCommand` 绑定到 UI command source。
- 监听 CanExecute 变化。
- 映射 busy / executing 状态。
- 与 Security、Routing 当前状态联动后的可执行性刷新。
- 释放 UI 事件订阅。

长耗时命令仍由 Mvvm / Core Operation 管理，Presentation 不执行后台任务调度。

### 4. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| Interaction completed | Unit | handler 返回结果。 |
| Interaction canceled | Unit | Scope 停用时返回 Canceled。 |
| Interaction missing | Unit | NotHandled 并记录诊断。 |
| Validation visual state | Unit | 验证错误映射到 UI 状态。 |
| Command CanExecute | Unit | UI command source 刷新可执行状态。 |
| 插件停用 | Unit | 插件 Interaction 取消并释放 handler。 |
