# AtomUI.City.Presentation View Locator 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `View Locator` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Presentation ViewLocator 设计

适用范围：ViewModel 到 ViewDescriptor 的定位、View manifest、重复 View 诊断和插件 View 撤销

### 1. 定位

ViewLocator 负责 `ViewModel -> ViewDescriptor`。

第一版不依赖运行时命名约定扫描。

### 2. 声明方式

推荐声明：

```csharp
[ViewFor(typeof(SettingsViewModel))]
public sealed partial class SettingsView : UserControl
{
}
```

Source Generator 生成 View manifest：

```text
ViewModelType
-> ViewType
-> Contribution
-> Resource scope
-> Factory descriptor
```

### 3. 规则

- 一个 ViewModel 默认只能有一个默认 View。
- 多 View 场景必须显式命名，例如 `ViewKey`。
- generated manifest 通过 `ViewRegistry.RegisterManifest` 原子注册；重复 key 失败时不得产生部分注册。
- 显式覆盖必须通过 `ViewRegistrationOptions.ReplaceExisting` 表达，默认重复注册继续拒绝。
- lookup 使用 ViewModel type + ViewKey 精确 dictionary key；不得 fallback 到反射扫描或 assignable type 扫描。
- ViewLocator 不创建 ViewModel。
- ViewLocator 不解释 Route。
- 插件 View 必须记录 PluginId 和 ContributionId。
- 插件卸载时必须撤销对应 View descriptor。

### 4. AOT 和 Source Generator

Presentation generator 负责：

- 生成 View/ViewModel binding manifest。
- 生成 View factory descriptor。
- 诊断重复默认 View。
- 诊断 ViewModel 没有 View。
- 诊断插件 View 类型泄漏。
- 诊断运行时扫描和命名约定定位。

运行时禁止扫描程序集找 View。

### 5. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| ViewLocator 命中 | Unit | ViewModel 定位到 ViewDescriptor。 |
| 找不到 View | Unit | 返回 commit failure 诊断。 |
| Manifest 注册 | Unit | 批量注册成功且失败无部分注册。 |
| 显式覆盖 | Unit | ReplaceExisting 替换已有 descriptor，默认重复仍拒绝。 |
| 多默认 View | Analyzer/Generator | 输出重复 View 诊断。 |
| 命名 View | Unit | ViewKey 能选择对应 View。 |
| 插件 View 撤销 | Unit | 撤销后不能定位插件 View。 |
