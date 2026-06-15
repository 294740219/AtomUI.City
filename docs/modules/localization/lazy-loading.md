# AtomUI.City.Localization Lazy Loading 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Lazy Loading` 相关实现决策，不重新定义模块边界。

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

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation concrete UI types` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Localization Lazy Loading 设计

适用范围：manifest-only startup、按当前语言包懒加载、active scope 资源加载、fallback 按需加载和缓存。

### 1. 定位

Localization 懒加载以语言包为单位。

它不是按单个 key 零散加载，也不是启动时加载所有语言包。启动阶段只加载 manifest，当前选择语言决定实际加载哪些 language package。

### 2. 启动流程

```text
Application startup
-> load localization manifests only
-> register package descriptors
-> select initial culture
-> load required active packages for selected culture
```

启动不加载：

- 未选择 culture 的 package。
- 未激活 Route 的 package。
- 未使用插件的 package。
- 所有 fallback package。

### 3. 加载策略

| 策略 | 说明 |
|---|---|
| Eager | Host 核心资源启动加载。 |
| OnDemand | 模块首次访问时加载。 |
| RouteActivated | 路由进入时预加载页面资源。 |
| PluginActivated | 插件启用时只注册 manifest，资源本体按需加载。 |
| CultureSwitch | 切换文化时按当前活动资源集合加载。 |
| PreloadHint | 模块声明预加载 hint。 |

### 4. 当前文化懒加载

示例：

```text
CurrentCulture = zh-CN
Active modules = Host, SettingsModule
Active plugin = SalesPlugin

Load:
Host.zh-CN
SettingsModule.zh-CN
SalesPlugin.zh-CN

Do not load:
Host.en-US
SettingsModule.ja-JP
SalesPlugin.en-US
```

### 5. Fallback 按需加载

Fallback chain 示例：

```text
zh-CN -> zh-Hans -> zh -> invariant
```

规则：

- 只有当前层缺失 key 时，才加载下一层 fallback package。
- fallback package 也按 contribution 加载。
- fallback 加载失败必须记录诊断。
- 同一 culture/package 的并发 load 必须合并为一个 in-flight task。
- package cache key 必须同时包含 culture 和 package id，避免同名 package 的不同 culture 互相污染。
- lookup 阶段 package load 失败时必须记录诊断，跳过该 package，并继续 fallback chain。

### 6. Active Package Set

Active package set 由当前运行时状态决定：

- ApplicationScope。
- WindowScope。
- NavigationScope。
- RouteScope。
- ActivationScope。
- Plugin contribution。
- Presentation resource scope。

Route 离开、Window 关闭、Plugin 停用时，对应 package 可释放或降级为 weak cache。

### 7. 缓存

缓存维度：

```text
Culture
ContributionId
PackageId
PackageVersion
ResourceRevision
```

规则：

- 当前活动 package 优先保留。
- 非活动 package 可被内存压力释放。
- culture switch 使 active cache revision 变化。
- plugin unload 清理插件 package cache。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| package not found | fallback 或 missing marker。 |
| package load failed | culture switch 阶段 rollback；普通 lookup 阶段 fallback。 |
| cache entry stale | 重新加载。 |
| plugin package revoked | 清理 cache 并 fallback。 |

### 9. 测试策略

测试必须覆盖：

- startup 只加载 manifest。
- 当前 culture package 加载。
- 未选择 culture package 不加载。
- fallback 按需加载。
- route activated 预加载。
- plugin activated 不加载所有语言包。
- plugin route opened 加载当前 culture package。
