# AtomUI.City.Localization Routing Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Routing Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Routing Integration 设计

适用范围：Route title、breadcrumb、错误路由、Resolver/Guard 文案、导航诊断和 route language package preload。

### 1. 定位

Routing 集成让路由 metadata 使用本地化 key，而不是固定显示文本。

Routing 不查找资源，不操作 UI。Localization 解析 key，Presentation 展示文本。

### 2. Route Metadata

Route 可以声明：

```text
TitleKey
DescriptionKey
BreadcrumbKey
GroupKey
ErrorTitleKey
```

Source Generator 将这些写入 Route descriptor。

### 3. 页面进入预加载

Route activated 可以触发当前 culture 的 route language package 预加载。

```text
Route matched
-> identify route localization package descriptors
-> load selected culture packages
-> continue Presentation binding
```

预加载失败按资源 criticality 决定是 fallback、missing marker 还是导航失败。

### 4. Guard / Resolver 文案

Guard、Resolver 不返回显示文本。

它们返回：

- ErrorCode。
- MessageKey。
- MessageArgs。
- Diagnostics。

Presentation 或 ViewModel 通过 Localization 渲染。

### 5. Culture 切换

文化切换后必须刷新：

- 当前 route title。
- breadcrumb。
- navigation menu。
- route error view。
- navigation diagnostics display。

Routing 的 NavigationSnapshot 不因 culture change 重新创建。

### 6. 插件路由

插件路由标题和 breadcrumb 使用插件本地化资源。

插件停用时：

- route contribution 撤销。
- language package 撤销。
- navigation UI fallback 或移除。

### 7. 测试策略

测试必须覆盖：

- Route title key。
- breadcrumb refresh。
- route language package preload。
- Guard message key。
- Resolver message key。
- 插件 route resource revoke。
