# AtomUI.City.Localization UI Refresh 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `UI Refresh` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。

## Public Contract

- 只允许通过 `AtomUI.City.Localization` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-LOCALIZATION-001 | Culture State | CultureStateTests |
| AUC-LOCALIZATION-002 | Language Package Providers | LanguagePackageProviderTests |
| AUC-LOCALIZATION-003 | Lazy Loading | LocalizationServiceTests |
| AUC-LOCALIZATION-004 | Lookup and Fallback | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Assembly Language Packages | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-006 | Presentation Bridge | LocalizationServiceTests |
| AUC-LOCALIZATION-007 | Plugin Package Revocation | LocalizationServiceTests |
| AUC-LOCALIZATION-008 | Generated Localization Manifest | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation concrete UI types` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Localization UI Refresh 设计

适用范围：文化变化后的 binding refresh、Window title、Route title、Command、Validation、Interaction 和错误文本刷新。

### 1. 定位

UI refresh 负责让文化变化后现有界面自动更新文本。

开发者不应该在每个 ViewModel 手写大量 `OnPropertyChanged`。Localization 应提供 culture-aware binding/reaction 机制。

### 2. 刷新链路

```text
Language packages loaded
-> CultureState committed
-> IPresentationLocalizationBridge.ApplyCultureAsync(batch state)
-> Presentation binding adapter refresh
-> AtomUI/Avalonia resources swapped
-> LocalizedText batch refresh
-> Command / Route / Validation text refresh
```

### 3. 刷新范围

必须支持刷新：

- `ILocalizedText` 和 formatted message text。
- Presentation `LocalizedTextBindingSet` 的任意 setter 目标。
- Presentation Window title。
- Presentation Route title、description、breadcrumb、group 和 error title。

Command、Validation、Dialog、Data/Security error 和 Notification 可以通过通用 setter/`ILocalizedText` 接入；专用 adapter 需要各 owning module 单独分配 Feature ID。

### 4. Culture-aware Binding

下列 XAML markup extension 仅为后续候选语法，当前 1.0 未实现：

```xml
<TextBlock Text="{loc:Text Settings.Title}" />
```

规则：

- 当前绑定句柄持有 `ILocalizedText.Changed` 订阅。
- View detached 后释放订阅。
- 插件 View 的 binding 随插件 UI 释放。
- Localization Core 的 `ILocalizedText.Changed` 在当前异步调用链执行；Presentation binding adapter 必须把实际 UI mutation 调度到 UI Thread。

### 5. ViewModel 文本

ViewModel 可以使用 `ILocalizedText` 或生成的 key constants；生成的强类型方法 accessor 尚不属于 1.0。

规则：

- `ILocalizedText` 可以随 culture change 刷新。
- 简单字符串属性由调用方通过 `LocalizedTextBindingSet` setter 或 `ILocalizedText.Changed` 更新。
- ViewModel 停用时释放 localization subscription。

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| binding key missing | missing marker + diagnostics。 |
| presentation bridge failed | 不回滚 CultureState；返回失败 result、记录诊断，并继续刷新本地 LocalizedText。 |
| refresh callback failed | 记录错误，不阻止其他 binding。 |
| View detached | 停止刷新。 |
| plugin resource revoked | fallback 或 clear UI。 |

### 7. 测试策略

测试必须覆盖：

- `ILocalizedText` 和通用 setter refresh。
- Window title refresh。
- Route metadata refresh。
- plugin View detached 后不刷新。
- missing key marker。
