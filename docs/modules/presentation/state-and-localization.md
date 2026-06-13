# AtomUI.City.Presentation State And Localization 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `State And Localization` 相关实现决策，不重新定义模块边界。

## 设计决策

- Presentation 负责 ViewModel -> View -> Outlet -> VisualTree。
- VisualTree 变化必须通过生命周期事件或绑定反馈回 ViewModel/State。
- View 创建和提交必须在 UI dispatcher 上执行。
- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。

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

## AtomUI.City.Presentation State 与 Localization 集成设计

适用范围：State UI 更新、culture refresh、binding refresh、路由标题和错误文本刷新

### 1. State UI 更新

State Core 不直接依赖 UI。Presentation 负责 UI 线程安全更新。

规则：

- UI 订阅使用 `DispatchPolicy.UiThread`。
- State 到 UI 的订阅必须绑定 Scope。
- View detached 后停止 UI 更新。
- 插件 View 相关订阅随插件停用释放。
- UI 更新异常进入 Presentation diagnostics。

Presentation 不保存应用状态；状态仍归 State 模块管理。

### 2. Localization UI 刷新

Presentation 负责把 Localization 的文化变化反映到 UI。

职责：

- 注册本地化 binding adapter。
- 当前文化变化后刷新文本。
- 路由标题、命令文本、验证消息、错误消息刷新。
- 插件本地化资源撤销后更新或清理对应 UI。

Localization 负责资源查找和文化状态，Presentation 负责 UI 展示刷新。

### 3. UI 线程规则

Culture refresh、ResourceDictionary 更新和 binding refresh 必须发生在 UI Thread。

```text
Culture state changed
-> Localization resolves resources
-> Presentation dispatcher
-> refresh bindings/resources
-> visual tree updated
```

### 4. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| State UI 订阅 | Unit | 通过 fake UI dispatcher 更新。 |
| View detached | Unit | detached 后停止 UI 更新。 |
| culture refresh | Unit | 文本 binding 被刷新。 |
| route title refresh | Unit | 路由标题随 culture 更新。 |
| 插件资源撤销 | Unit | 插件文本资源撤销后 UI 清理或 fallback。 |
