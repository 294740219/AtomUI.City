# AtomUI.City.Presentation Resources And Plugins 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Resources And Plugins` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
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

## AtomUI.City.Presentation 资源与插件设计

适用范围：AtomUI/Avalonia 资源、主题、插件 View、插件资源贡献和撤销

### 1. Resource 和 Theme 集成

Presentation 接入 AtomUI/Avalonia 资源系统。

资源类型：

- Styles。
- Themes。
- Icons。
- Templates。
- Fonts。
- Images。
- Localization resource bridge。

插件资源必须通过 ContributionLease 进入 `IPresentationResourceRegistry`。

### 2. 插件贡献

插件可以贡献：

- View。
- Style。
- Theme resource。
- Icon。
- Data template。
- Interaction handler。
- Presentation resource。

### 3. 插件贡献规则

- 必须有 ContributionLease。
- 必须记录 PluginId。
- 必须可撤销。
- 不能污染 Host Root resource registry。
- 不能让 Host 静态缓存持有插件私有 View 类型实例。
- 停用时必须先停止新入口，再关闭活动 UI，再撤销资源。

插件 View/ViewModel 绑定中跨边界传递的公共类型必须位于 Host 共享 contract 程序集。

### 4. 停用流程

```text
Stop new view creation from plugin
-> Detach active plugin views
-> Remove plugin resources
-> Clear resource cache
-> Dispose plugin resource scope
```

### 5. AOT 和 Source Generator

Presentation generator 负责：

- 生成 Resource manifest。
- 生成 Interaction handler descriptor。
- 生成 Validation binding descriptor。
- 诊断插件 View 类型泄漏。
- 诊断运行时资源扫描。

### 6. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| resource contribution | Unit | 资源通过 lease 注册。 |
| resource revoke | Unit | lease 撤销后资源不可用。 |
| plugin View close | Unit | 插件停用关闭活动 View。 |
| Host root 污染 | Unit/Analyzer | 插件资源不进入 Host root registry。 |
| 插件类型泄漏 | Analyzer/Generator | 输出稳定诊断。 |
