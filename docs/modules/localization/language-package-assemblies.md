# AtomUI.City.Localization Language Package Assemblies 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Language Package Assemblies` 相关实现决策，不重新定义模块边界。

## 设计决策

- 包布局必须可由测试断言。
- 路径必须使用跨平台分隔符处理。
- 安装目录不得允许路径穿越。
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

## AtomUI.City.Localization Language Package Assemblies 设计

适用范围：独立语言包 assembly、satellite assembly、collectible AssemblyLoadContext、ResourceManager、Native AOT locpack fallback 和卸载约束。

### 1. 定位

普通 .NET 桌面运行时下，Localization 支持语言包放在独立 assembly 中运行时动态加载。

语言包 assembly 推荐是 resource-only assembly，不放可执行代码。它只承载指定 culture 的资源数据，不承载业务逻辑。

Native AOT 不支持运行时动态加载 assembly，因此必须提供 file-based locpack fallback。

### 2. Provider 模型

统一抽象：

```text
ILanguagePackageProvider
-> AssemblyLanguagePackageProvider
-> FileLanguagePackageProvider
```

| Provider | 场景 |
|---|---|
| `AssemblyLanguagePackageProvider` | CoreCLR、普通桌面运行时、插件动态加载。 |
| `FileLanguagePackageProvider` | Native AOT、严格 trimming、独立资源包。 |

两者都输出同一套 `ILanguagePackage` / `ILocalizedResourceStore` contract。

### 3. Assembly 语言包布局

推荐使用 .NET satellite assembly 风格：

```text
locales/
  zh-CN/
    AtomUI.City.App.resources.dll
    SettingsModule.resources.dll
    SalesPlugin.resources.dll
  en-US/
    AtomUI.City.App.resources.dll
    SettingsModule.resources.dll
    SalesPlugin.resources.dll
```

也允许自定义命名：

```text
SalesModule.Localization.zh-CN.dll
SalesModule.Localization.en-US.dll
```

但无论命名如何，descriptor 必须说明：

- Culture。
- PackageId。
- Assembly path。
- Resource base name。
- ContributionId。
- Version。
- Checksum。

### 4. 加载流程

```text
Language package descriptor
-> resolve package path
-> load assembly in package load context
-> create resource store
-> expose lookup table / ResourceManager adapter
-> register package lease
```

Host app 语言包可以加载到默认上下文或专用上下文。插件语言包必须跟插件加载上下文和 ContributionLease 绑定。

### 5. 卸载约束

如果语言包需要随插件卸载，必须避免外部强引用。

禁止：

- Host 静态缓存插件语言包 assembly。
- Host 静态缓存插件 `ResourceManager`。
- Host 静态缓存插件 localizer delegate。
- Host 持有插件语言包里的 Type。
- AtomUI/Avalonia ResourceDictionary 未移除就卸载插件。
- generated accessor 类型放进语言包 assembly。

强类型 accessor 应生成在模块或插件主 assembly。语言包 assembly 只提供资源数据。

### 6. Native AOT Locpack

Native AOT 模式使用 file-based locpack。

可选格式：

- `.locpack` binary。
- json。
- embedded generated table。

规则：

- locpack 不依赖动态 assembly loading。
- locpack descriptor 与 assembly package descriptor 语义一致。
- Source Generator 生成 locpack manifest。
- 运行时通过 `FileLanguagePackageProvider` 加载当前 culture 的 locpack。

### 7. 安全和完整性

语言包 assembly / locpack 应支持：

- checksum。
- version。
- culture metadata。
- package compatibility。
- plugin ownership。

语言包不作为安全边界。不可信语言包仍然必须受 Host 安全策略和插件策略约束。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| assembly 不存在 | fallback 或 culture switch rollback。 |
| assembly 加载失败 | fallback 或 rollback。 |
| checksum 不匹配 | 拒绝加载。 |
| culture 不匹配 | 拒绝加载。 |
| AOT 下请求 assembly provider | 返回不支持，使用 file provider。 |
| 卸载后仍有引用 | 标记 UnloadPending，输出诊断。 |

### 9. 测试策略

测试必须覆盖：

- assembly package 加载。
- satellite-style package 解析。
- file locpack 加载。
- AOT provider fallback。
- checksum mismatch。
- plugin package unload。
- Host 不持有插件 package 引用。
