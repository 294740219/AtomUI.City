# AtomUI.City.Localization Resource Model 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Resource Model` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Localization Resource Model 设计

适用范围：Resource descriptor、resource scope、language package、资源类型、资源分层和 contribution。

### 1. 定位

Resource model 描述本地化资源如何被声明、索引、加载和查找。

Localization 不是简单的字符串字典。它需要表达 Host、Module、Plugin、Route、Theme 和 Presentation 的资源贡献关系，并支持按 culture 懒加载。

### 2. Resource Descriptor

Resource descriptor 应包含：

| 字段 | 说明 |
|---|---|
| ResourceId | 稳定资源标识。 |
| Key | 资源 key。 |
| ResourceType | String、FormattedString、Object、FlowDirection 等类型。 |
| Culture | 所属 culture。 |
| Scope | Host、Module、Plugin、Route、Window 等资源范围。 |
| Contribution | 来源 Contribution。 |
| PackageId | 语言包 id。 |
| Version | 资源版本。 |
| FallbackPolicy | fallback 策略。 |

运行时不通过扫描程序集发现 descriptor，默认消费 Source Generator manifest。

### 3. Resource Scope

资源 Scope：

| Scope | 说明 |
|---|---|
| Host | 应用全局资源。 |
| Module | 模块资源。 |
| Plugin | 插件资源。 |
| Route | 页面或路由资源。 |
| Window | 窗口级资源。 |
| Presentation | AtomUI/Avalonia UI 资源桥。 |

资源 Scope 决定加载时机、查找优先级和撤销边界。

### 4. Language Package

Language package 是懒加载基本单位。

规则：

- 每个 package 只包含一个 culture。
- package 可以来自独立 assembly 或 file-based locpack。
- package 必须有 descriptor。
- package 加载后产生 `ILocalizedResourceStore`。
- package 必须支持释放。

推荐命名：

```text
Host.zh-CN
SettingsModule.zh-CN
SalesPlugin.zh-CN
```

### 5. 资源类型

第一版必须支持字符串，架构预留更多类型：

| 类型 | 用途 |
|---|---|
| String | 普通文本。 |
| FormattedString | 参数化文本。 |
| Pluralization | 数量规则，后续增强。 |
| ResourceObject | 图片、图标、字体、文档片段。 |
| FlowDirection | RTL / LTR。 |
| CultureMetadata | 日期、数字、货币格式 metadata。 |
| ValidationMessage | 表单验证消息。 |
| ErrorMessage | 错误展示。 |
| CommandText | 菜单、按钮、快捷入口。 |
| RouteTitle | 页面标题、面包屑。 |

### 6. 资源分层

查找层级：

```text
route resources
-> window resources
-> plugin resources
-> module resources
-> host resources
-> presentation framework resources
-> fallback culture
-> invariant fallback
-> missing resource marker
```

同一 scope 内保持 Host 注册顺序；Host 可以通过 descriptor 注册顺序和 contribution policy 控制同级覆盖。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| descriptor 重复 | 构建期诊断。 |
| resource type 不匹配 | fallback，并记录诊断。 |
| package version 不兼容 | 拒绝加载 package。 |
| contribution 已撤销 | 拒绝查找或 fallback。 |

### 8. 测试策略

测试必须覆盖：

- Host / Module / Plugin descriptor。
- Resource scope 查找优先级。
- package 版本不兼容。
- 插件资源撤销。
- resource type mismatch。
