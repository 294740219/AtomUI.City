# AtomUI.City.Localization Atomui Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Atomui Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization AtomUI Integration 设计

适用范围：Presentation bridge、AtomUI culture adapter、Avalonia ResourceDictionary、UI Thread、FlowDirection 和资源撤销。

### 1. 定位

Localization 最终需要反映到 AtomUI/Avalonia UI。

Localization Core 不直接引用 AtomUI/Avalonia。Presentation 负责提供 `IPresentationLocalizationBridge`，把文化状态和资源变化同步到 AtomUI/Avalonia。

### 2. 集成链路

```text
LocalizationService.SetCultureAsync
-> load selected language packages
-> commit CultureState
-> IPresentationLocalizationBridge.ApplyCultureAsync
-> update AtomUI culture
-> update Avalonia ResourceDictionary
-> notify localized bindings
```

AtomUI/Avalonia resource apply 必须发生在 UI Thread。

### 3. Bridge 职责

`IPresentationLocalizationBridge` 负责：

- 接收 CultureState。
- 构建或更新 localized ResourceDictionary。
- 同步 AtomUI culture。
- 应用 FlowDirection。
- 刷新 localized binding。
- 撤销插件资源字典。
- 输出 UI refresh diagnostics。

### 4. ResourceDictionary Scope

资源字典挂载建议：

| 来源 | 挂载位置 |
|---|---|
| Host 核心语言包 | Application resources。 |
| Window 语言资源 | Window resources。 |
| Route 页面语言资源 | Route / View resource scope。 |
| Plugin 语言资源 | Plugin contribution resource scope。 |
| Theme / AtomUI 文案 | Presentation / AtomUI bridge。 |

禁止把所有模块和插件语言资源都塞进全局 Application resources。

### 5. FlowDirection

Culture metadata 可以影响 FlowDirection。

规则：

- RTL/LTR 变化由 Presentation bridge 应用。
- FlowDirection 变化必须触发布局刷新。
- 不支持 RTL 的 View 可以声明限制，诊断必须可见。

### 6. 插件撤销

插件停用时：

```text
Block new localization lookup
-> detach plugin UI
-> remove plugin ResourceDictionary
-> clear AtomUI resource references
-> revoke language package
-> emit diagnostics
```

撤销后 AtomUI/Avalonia 不得继续持有插件语言包 assembly 或 ResourceDictionary。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| UI dispatcher 未 ready | 等待 ready 或返回明确错误。 |
| ResourceDictionary apply failed | bridge 返回失败；Localization 保留已提交 CultureState、记录诊断并继续本地文本刷新。 |
| FlowDirection apply failed | 保留旧方向并记录诊断。 |
| 插件资源字典移除失败 | 进入插件卸载错误聚合。 |

### 8. 测试策略

测试必须覆盖：

- bridge apply culture。
- resource dictionary swap。
- UI Thread enforcement。
- FlowDirection change。
- plugin resource dictionary revoke。
- apply failed 的局部资源一致性由 Presentation bridge 承担，Localization 不回滚 CultureState。
