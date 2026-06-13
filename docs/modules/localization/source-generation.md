# AtomUI.City.Localization Source Generation 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Source Generation` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。
- 优先 source generator，避免运行时反射扫描。
- generated output 必须稳定排序。
- diagnostics 必须可由 generator tests 断言。

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

## AtomUI.City.Localization Source Generation 设计

适用范围：Resource manifest、language package descriptor、强类型 key、accessor、AOT、Analyzer 和构建期诊断。

### 1. 定位

Localization 是 source-generator-first 模块。

运行时默认不扫描程序集找资源，也不靠反射发现资源 key。Source Generator 生成 manifest、descriptor 和强类型访问入口。

### 2. 生成内容

Generator 负责：

- Resource manifest。
- Language package descriptor。
- Supported culture manifest。
- Fallback culture manifest。
- Strongly typed accessor。
- Key constants。
- Module resource descriptor。
- Plugin resource descriptor。
- AtomUI resource bridge descriptor。
- Locpack manifest。

### 3. 强类型 Accessor

强类型 accessor 生成在模块或插件主 assembly 中，不生成在语言包 assembly 中。

原因：

- 语言包 assembly 应保持 resource-only。
- 插件语言包卸载不能影响主插件 API。
- Host 不应持有语言包 assembly 类型。

### 4. Analyzer 诊断

必须诊断：

- 重复 key。
- 未声明 key 引用。
- fallback 不完整。
- invariant 缺失。
- culture package 缺失。
- 格式化参数数量不匹配。
- 插件资源覆盖 Host key。
- 插件资源类型泄漏。
- 运行时反射式资源扫描。

### 5. AOT

Native AOT 模式：

- 禁止依赖动态 assembly loading。
- 使用 file-based locpack。
- manifest 和 accessor 仍由 generator 生成。
- 运行时消费强类型 descriptor。

### 6. Build 集成

Build 模块后续负责：

- 生成 language package assembly。
- 生成 locpack。
- 输出 manifest。
- 校验资源完整性。
- 复制 culture package 到 output。

Localization 文档只定义 contract。

### 7. 测试策略

测试必须覆盖：

- manifest 生成。
- accessor 生成。
- duplicate key 诊断。
- missing key 诊断。
- fallback incomplete 诊断。
- locpack manifest。
- plugin resource leakage 诊断。
