# AtomUI.City.Presentation Dispatcher 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Dispatcher` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。
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

## AtomUI.City.Presentation UI Dispatcher 设计

适用范围：`IUiDispatcher`、UI thread access、投递、停止、异常和测试

### 1. 定位

Presentation 提供 `IUiDispatcher` 的 Avalonia 实现。

Core 只定义调度抽象，不依赖 Avalonia Dispatcher。

### 2. 基本规则

- `CheckAccess` 映射 Avalonia UI thread access。
- `InvokeAsync` 返回执行结果或异常。
- `PostAsync` 表示异步投递。
- UI runtime 未 ready 时，按 Host 策略等待或返回明确错误。
- UI runtime stopping 后拒绝新投递。
- Dispatcher callback 异常进入 ErrorPolicy。
- 插件不能长期静态保存 dispatcher callback。

调度策略见：[Core Threading 设计](../core/threading.md)。

### 3. 与 State/EventBus 集成

State 和 EventBus 不直接依赖 Avalonia。

```text
StateDispatchPolicy.Dispatcher / EventDispatchPolicy.UiThread
-> IUiDispatcher
-> Presentation Avalonia dispatcher
-> UI callback
```

UI callback 必须绑定 Scope。Scope 停止后，未执行 callback 应取消或跳过。

### 4. 错误策略

| 场景 | 默认处理 |
|---|---|
| UI dispatcher 未 ready | 等待 ready 或返回明确错误。 |
| UI dispatcher stopping | 拒绝投递。 |
| callback 抛异常 | 进入 Presentation diagnostics 和 ErrorPolicy。 |
| callback 所属 Scope 已停止 | 跳过并记录 trace 级诊断。 |

### 5. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| CheckAccess | Unit | UI 线程和非 UI 线程结果正确。 |
| InvokeAsync | Unit | 返回 callback 结果。 |
| PostAsync | Unit | fake dispatcher 可 drain。 |
| stopping 拒绝 | Unit | 停止后投递失败。 |
| callback 异常 | Unit | 诊断记录。 |
| Scope 停止 | Unit | 停止后的 callback 不执行。 |
