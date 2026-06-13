# AtomUI.City.Presentation UI Runtime 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `UI Runtime` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Presentation UI Runtime 设计

适用范围：UI runtime ready/stopping、PresentationScope、WindowScope 和 AtomUI/Avalonia runtime bridge

### 1. 定位

Presentation 负责把 AtomUI.City Host 连接到 AtomUI/Avalonia UI runtime。

Core 不依赖 Avalonia。Presentation 是 Core 和 Avalonia 之间的适配层。

### 2. 启动链路

```text
ApplicationHost
-> StartPresentation
-> Initialize AtomUI/Avalonia runtime
-> Register IUiDispatcher
-> Create PresentationScope
-> Open initial WindowScope
-> Routing navigates initial route
```

### 3. 职责

Presentation runtime 负责：

- 初始化 Avalonia application bridge。
- 接入 AtomUI 资源和主题。
- 注册 Core `IUiDispatcher`。
- 报告 UI runtime ready。
- 创建 PresentationScope。
- 创建 WindowScope。
- 在 UI runtime 停止时拒绝新的 UI 投递。
- 输出 UI runtime 诊断。

### 4. PresentationScope

PresentationScope 是 UI runtime 的生命周期边界。

规则：

- PresentationScope 由 Host 生命周期管理。
- WindowScope 必须是 PresentationScope 的子 Scope。
- UI runtime stopping 后，不能创建新的 WindowScope。
- PresentationScope 停止时释放所有窗口、Outlet、View、binding、Interaction handler 和 UI 订阅。

### 5. WindowScope

每个窗口有独立 WindowScope。

规则：

- 一个 WindowScope 下可以有一个或多个 NavigationScope。
- 窗口关闭先请求 Routing/Mvvm leave confirmation。
- 关闭确认通过 Mvvm Interaction 或 Leave Guard 处理。
- WindowScope 停止时释放窗口内所有 UI contribution。

Presentation 不定义业务窗口模型，只提供窗口生命周期桥接。

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| UI runtime 启动失败 | Application fatal。 |
| UI runtime 未 ready | 按 Host 策略等待或返回明确错误。 |
| UI runtime stopping 后新投递 | 拒绝并记录诊断。 |
| WindowScope 释放失败 | 聚合错误，继续释放其他窗口。 |

### 7. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| runtime ready | Unit | ready 后 dispatcher 可用。 |
| runtime stopping | Unit | 停止后拒绝新 UI 投递。 |
| PresentationScope stop | Unit | 子 WindowScope 被释放。 |
| Window close intent | Unit | 转成 leave request 或 interaction。 |
| 启动失败 | Unit | 返回 fatal 诊断。 |
