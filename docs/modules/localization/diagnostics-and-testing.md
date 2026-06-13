# AtomUI.City.Localization Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## AtomUI.City.Localization Diagnostics and Testing 设计

适用范围：缺失资源诊断、加载失败、fallback、culture switch、AtomUI bridge、插件撤销和测试工具。

### 1. 定位

Localization 必须可诊断、可测试。

缺失文本不能只表现为 UI 空白。必须能说明缺的是哪个 key、哪个 culture、哪个 package、哪个 contribution 和哪个 fallback 阶段。

### 2. 诊断字段

必须记录：

- Culture。
- Fallback culture。
- Resource key。
- Resource type。
- PackageId。
- Package version。
- Scope。
- ModuleId。
- PluginId。
- ContributionId。
- Lookup stage。
- Missing reason。
- Load duration。
- Apply duration。
- Culture revision。

敏感信息通常不应放入本地化资源 key。错误参数写入诊断时需要脱敏。

### 3. 诊断分类

| 分类 | 说明 |
|---|---|
| ResourceMissing | 当前 culture 缺 key。 |
| FallbackMissing | fallback 也缺 key。 |
| PackageLoadFailed | 语言包加载失败。 |
| PackageVersionMismatch | 语言包版本不兼容。 |
| FormatFailed | 格式化失败。 |
| ResourceRevoked | 资源 contribution 已撤销。 |
| AtomUiApplyFailed | AtomUI/Avalonia 资源应用失败。 |
| CultureSwitchRolledBack | 文化切换已回滚。 |
| PluginResourceLeak | 插件资源仍被引用。 |

### 4. Testing 包

Testing 包应提供：

- Fake culture state provider。
- Fake language package provider。
- Fake assembly package provider。
- Fake locpack provider。
- Test localization service。
- Test presentation localization bridge。
- Missing resource recorder。
- Culture switch driver。
- Plugin localization test host。
- Resource leak assertion helper。

### 5. 测试场景

必须覆盖：

- manifest-only startup。
- selected culture package lazy load。
- fallback package lazy load。
- culture switch success。
- culture switch rollback。
- missing marker。
- format error。
- AtomUI bridge apply。
- UI binding refresh。
- plugin package revoke。
- plugin assembly unload。
- AOT locpack provider。

### 6. 无 UI 测试

Localization Core 测试不依赖真实 AtomUI/Avalonia。

Presentation integration 测试使用 fake bridge。真实 UI resource dictionary 测试放到 Presentation 平台集成测试中。
