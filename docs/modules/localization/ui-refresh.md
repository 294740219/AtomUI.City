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
| AUC-LOCALIZATION-003 | Resource Declarations | LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-004 | Lookup and Fallback | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Lazy Loading | LocalizationServiceTests |
| AUC-LOCALIZATION-006 | Presentation Bridge | LocalizationServiceTests |

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
CultureState committed
-> LocalizationChanged notification
-> Presentation binding adapter refresh
-> AtomUI/Avalonia resources swapped
-> View text updates
-> Command / Route / Validation text refresh
```

### 3. 刷新范围

必须支持刷新：

- XAML localized binding。
- Window title。
- Route title。
- Breadcrumb。
- Command text。
- Command tooltip。
- Validation message。
- Dialog / Interaction 文案。
- Data / Security error message。
- Notification / Toast 文案。

### 4. Culture-aware Binding

XAML 目标语法：

```xml
<TextBlock Text="{loc:Text Settings.Title}" />
```

规则：

- Binding 订阅 CultureState revision。
- View detached 后释放订阅。
- 插件 View 的 binding 随插件 UI 释放。
- Binding refresh 必须在 UI Thread。

### 5. ViewModel 文本

ViewModel 可以使用 `ILocalizedText` 或强类型 accessor。

规则：

- `ILocalizedText` 可以随 culture change 刷新。
- 简单字符串属性可以由 source generator 生成 culture-aware notification。
- ViewModel 停用时释放 localization subscription。

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| binding key missing | missing marker + diagnostics。 |
| refresh callback failed | 记录错误，不阻止其他 binding。 |
| View detached | 停止刷新。 |
| plugin resource revoked | fallback 或 clear UI。 |

### 7. 测试策略

测试必须覆盖：

- XAML binding refresh。
- Window title refresh。
- Command text refresh。
- Validation message refresh。
- plugin View detached 后不刷新。
- missing key marker。
