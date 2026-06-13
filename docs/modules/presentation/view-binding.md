# AtomUI.City.Presentation View Binding 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `View Binding` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Presentation View 绑定设计

适用范围：View 创建、DataContext、binding handle、View/ViewModel 生命周期和释放

### 1. 定位

View binding 把 ViewModel instance 安全绑定到 View，并把 View 侧资源挂入 ActivationScope。

```text
ViewModel instance
-> IViewLocator locate ViewDescriptor
-> IViewFactory create View on UI Thread
-> IViewBinder set DataContext
-> attach lifecycle adapter
-> register disposables into ActivationScope
-> return BoundViewHandle
```

### 2. ViewFactory

规则：

- View 创建必须在 UI Thread。
- View 可以从 Application 或 Plugin service context 创建。
- View 构造函数不应启动长期任务。
- View 不能持有插件服务到 Host 静态对象。
- 创建失败返回 Presentation commit failure。

Strict AOT 模式下，ViewFactory 应由 Source Generator 生成强类型工厂，避免反射构造。

### 3. Binding 规则

- ViewModel 不知道 View 类型。
- View 不负责导航决策。
- Binding 必须可释放。
- ViewDataContext 变化必须受控，不能被外部任意覆盖。
- View 和 ViewModel 生命周期不完全等同，但必须有关联释放策略。
- UI 事件订阅、binding disposable 和 visual adapter 默认挂 ActivationScope。

### 4. 失败处理

Presentation 应提供诊断：

- 找不到 View。
- 找到多个默认 View。
- View 创建失败。
- Binding 失败。
- 插件 View descriptor 已撤销。

Binding 失败时，Presentation 必须释放已创建 View 和 provisional ActivationScope，并让 Routing 保持旧 Outlet 内容。

### 5. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| View 创建 | Unit | ViewFactory 在 fake UI dispatcher 上创建 View。 |
| DataContext 设置 | Unit | View 绑定到 ViewModel。 |
| Binding 释放 | Unit | ActivationScope 停止时释放 binding。 |
| View 创建失败 | Unit | commit failure，旧内容保留。 |
| 插件 View 泄漏 | Analyzer/Generator | 输出稳定诊断。 |
