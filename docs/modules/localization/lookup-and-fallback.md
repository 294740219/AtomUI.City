# AtomUI.City.Localization Lookup And Fallback 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Lookup And Fallback` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Lookup and Fallback 设计

适用范围：资源查找、资源优先级、fallback culture、missing marker、格式化错误和撤销后 fallback。

### 1. 定位

Lookup and fallback 负责把 resource key 解析为当前 culture 下的显示资源。

查找必须可诊断、可预测、可测试。

### 2. 查找输入

查找输入包含：

- Resource key。
- Resource type。
- Current culture。
- Scope。
- ModuleId。
- PluginId。
- ContributionId。
- Format args。
- Fallback policy。

### 3. 查找顺序

```text
Current feature / plugin
-> owning module
-> application host
-> shared framework
-> fallback culture
-> invariant fallback
-> missing marker
```

同一层级内的优先级由 Contribution order 和 Host policy 决定。

### 4. Fallback Culture

Fallback chain 示例：

```text
zh-CN -> zh-Hans -> zh -> invariant
```

规则：

- fallback package 按需加载。
- fallback 命中必须记录诊断级别信息。
- critical resource fallback 失败可以导致 culture switch rollback。
- 非 critical resource fallback 失败返回 missing marker。

### 5. Missing Marker

开发模式默认：

```text
!Settings.Title!
```

发布模式默认：

- invariant fallback。
- key fallback。
- diagnostics record。

具体策略由 Host 配置。

### 6. 格式化

格式化资源必须使用当前 culture。

规则：

- 参数数量不匹配时返回 raw template 或 missing marker。
- 格式化异常记录 diagnostics。
- 日期、数字、货币使用 CurrentCulture。
- UI 文案资源使用 CurrentUICulture。

### 7. 资源撤销

插件资源撤销后：

```text
Revoke contribution
-> remove package store
-> invalidate lookup cache
-> fallback to owning module / host
-> notify Presentation refresh or clear UI
```

撤销后的资源不能继续被 Host cache 命中。

### 8. 测试策略

测试必须覆盖：

- 当前 scope 命中。
- module fallback。
- host fallback。
- culture fallback。
- invariant fallback。
- missing marker。
- 格式参数错误。
- 插件撤销后 fallback。
