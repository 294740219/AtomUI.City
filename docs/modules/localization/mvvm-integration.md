# AtomUI.City.Localization MVVM Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `MVVM Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization MVVM Integration 设计

适用范围：ViewModel localizer、强类型 accessor、Command 文本、Interaction、Validation 和 ActivationScope 绑定。

### 1. 定位

MVVM 集成让 ViewModel、Command、Interaction 和 Validation 使用统一本地化能力。

Mvvm 不实现资源查找。Localization 提供文本和 culture notification。Presentation 负责 UI 展示刷新。

### 2. ViewModel Localizer

字符串 key 模式：

```csharp
public sealed partial class SettingsViewModel
{
    private readonly IStringLocalizer<SettingsViewModel> _localizer;

    public string Title => _localizer["Settings.Title"];
}
```

强类型模式：

```csharp
public string Title => _texts.Settings.Title();
```

### 3. ActivationScope

Localization subscription 必须绑定 `ActivationScope`。

规则：

- ViewModel 激活时订阅 culture change。
- ViewModel 停用时释放订阅。
- ViewModel 构造函数不启动长期订阅。
- 插件 ViewModel 的 localizer 不泄漏到 Host 静态缓存。

### 4. Command

Command metadata 可以声明：

```text
TextKey
ToolTipKey
DescriptionKey
IconKey
```

Culture 变化后：

```text
CultureChanged
-> command text provider refresh
-> Presentation updates menu / toolbar / shortcut UI
```

Command 可执行性不由 Localization 决定。

### 5. Interaction

Interaction request 不应传固定显示文本。

推荐传：

- TitleKey。
- MessageKey。
- ButtonKey。
- MessageArgs。

Presentation handler 在显示时查找当前 culture 文本。

### 6. Validation

Validation message 应使用 MessageKey + MessageArgs。

文化切换后，仍显示的 validation message 必须可刷新。

### 7. 测试策略

测试必须覆盖：

- ViewModel localizer lookup。
- strong typed accessor。
- ActivationScope 停用释放 subscription。
- Command text culture refresh。
- Interaction message refresh。
- Validation message refresh。
