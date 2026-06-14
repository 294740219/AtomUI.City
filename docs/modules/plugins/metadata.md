# AtomUI.City.PluginSystem Metadata 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Metadata` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。

## Public Contract

- 只允许通过 `AtomUI.City.PluginSystem` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-PLUGIN-001 | Plugin Metadata | PluginDeclarationAttributeTests; PluginManifestTests |
| AUC-PLUGIN-002 | Dependency Validation | PluginDependencyTests |
| AUC-PLUGIN-003 | Package Installation | PluginPackageTests |
| AUC-PLUGIN-004 | Discovery | PluginLoadingTests |
| AUC-PLUGIN-005 | Loading | PluginLoadingTests |
| AUC-PLUGIN-006 | MSBuild Contract | PluginMsBuildContractTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## PluginSystem 元数据设计

适用范围：插件身份、清单、版本、兼容性、能力声明、安装记录和锁定信息

### 1. 目标

插件元数据必须让 Host 在不执行插件代码的前提下判断插件是否可信、兼容、可安装、可加载和可启用。

元数据设计目标：

- 插件身份稳定。
- 插件包和插件运行时身份分离。
- 插件兼容性可在加载前判断。
- 插件能力可在启用前授权。
- 插件安装状态可复现。
- 插件更新和回滚可追踪。
- 插件信息对 AOT 和 trimming 友好。

本篇是元数据总览。具体拆分见：

- [清单 Schema 设计](manifest-schema.md)
- [兼容性设计](compatibility.md)
- [能力授权设计](capabilities.md)
- [贡献索引设计](contribution-index.md)
- [包布局设计](package-layout.md)
- [签名和信任设计](signing-and-trust.md)

### 2. 身份模型

插件身份分为三个层次：

| 身份 | 用途 | 稳定性 |
|---|---|---|
| `PluginId` | 运行时插件身份、能力授权、配置隔离、状态隔离。 | 必须跨版本稳定。 |
| `PackageId` | NuGet 包身份、下载、缓存、包来源追踪。 | 可与 `PluginId` 不同。 |
| `MainAssemblyName` | 插件主业务程序集名称。 | 只用于加载和诊断，不作为业务身份。 |

规则：

- 一个插件包第一版只允许声明一个 `PluginId`。
- 一个插件包第一版只允许包含一个主业务程序集。
- 一个主业务程序集可以包含多个插件模块。
- `PluginId` 推荐使用反向域名格式，例如 `com.company.sales`。
- `PluginId` 一旦发布不应变更。
- 同一个 `PluginId` 可以安装多个版本，但同一插件配置 profile 内同一时间只能启用一个版本。

`PackageId` 不应被框架当作运行时身份。包名可以因发布渠道、品牌或迁移发生变化，但 `PluginId` 必须保持稳定。

`PluginManifestValidator` 必须在加载插件程序集前校验 schema、required fields、version、mainAssembly 和 targetFramework。

### 3. PluginProfile

插件安装目录必须按插件兼容 profile 隔离。

`PluginProfile` 由 Host 插件 API 兼容版本和渠道组成：

```text
<HostPluginApiVersion>-<Channel>
```

示例：

```text
1.0-stable
1.0-dev
2.0-stable
```

规则：

- `PluginProfile` 不等同于应用完整版本号。
- 应用 patch 升级不应导致插件目录整体迁移。
- Host 插件 API 或插件 ABI 发生破坏性变化时，应切换 `PluginProfile`。
- 不同渠道的插件目录必须隔离，例如 stable、beta、dev。

### 4. 插件清单

插件包必须包含框架清单：

```text
atomui-city/plugin.json
```

建议结构：

```json
{
  "schemaVersion": "1.0",
  "pluginId": "com.company.sales",
  "packageId": "Company.Sales.Plugin",
  "version": "1.0.0",
  "displayNameKey": "SalesPlugin.DisplayName",
  "descriptionKey": "SalesPlugin.Description",
  "publisher": "Company",
  "mainAssembly": "Company.Sales.Plugin.dll",
  "minHostVersion": "1.0.0",
  "pluginApiVersion": "1.0",
  "targetFramework": "net10.0",
  "aotCompatible": false,
  "unloadable": true,
  "capabilities": [
    {
      "name": "routes",
      "scope": ["/sales/**"]
    },
    {
      "name": "localization"
    }
  ],
  "contributions": {
    "routes": {
      "path": "manifests/routes.json",
      "required": true
    },
    "localization": {
      "path": "manifests/localization.json",
      "required": false
    }
  }
}
```

规则：

- `schemaVersion` 的未知主版本必须拒绝。
- `mainAssembly` 必须指向包内唯一主业务程序集。
- `displayNameKey` 和 `descriptionKey` 使用本地化 key，不直接写死展示文本。
- 清单读取不能要求加载插件程序集。
- 清单字段顺序由构建任务稳定生成，便于 hash 和审计。

### 5. 版本和兼容性

插件至少声明：

| 字段 | 用途 |
|---|---|
| `version` | 插件自身版本。 |
| `minHostVersion` | 最小 Host 版本。 |
| `maxHostVersion` | 最大 Host 版本，默认可省略。 |
| `pluginApiVersion` | Host 插件 API 兼容版本。 |
| `targetFramework` | 插件目标框架。 |
| `runtimeIdentifiers` | 插件携带 native/RID 资产时声明。 |
| `contractVersions` | 插件依赖的共享 contract 版本。 |

兼容性检查必须发生在加载前：

```text
Read manifest
-> Check schema version
-> Check Host version
-> Check plugin API version
-> Check target framework
-> Check contract versions
-> Check RID/native asset compatibility
```

不兼容的插件进入 `Invalid` 或 `Disabled`，不能加载程序集。

### 6. 能力声明

插件能力声明表达插件希望使用哪些扩展点。声明不等于授权。

示例：

```json
{
  "capabilities": [
    {
      "name": "routes",
      "scope": ["/sales/**"]
    },
    {
      "name": "data.http",
      "clients": ["SalesApi"]
    },
    {
      "name": "eventbus.subscribe",
      "contracts": ["SalesOrderChanged"]
    },
    {
      "name": "localization"
    }
  ]
}
```

规则：

- Host Security 或 Host policy 负责把 requested capabilities 转换为 granted capabilities。
- 插件启用时只能提交已授权能力范围内的 Contribution。
- 能力拒绝不一定导致插件安装失败，但会阻止对应 Contribution。
- 能力校验结果必须进入诊断。

### 7. Contribution Index

插件清单只描述贡献清单的位置，不直接内联所有贡献内容。

建议：

```json
{
  "contributions": {
    "routes": {
      "path": "manifests/routes.json",
      "required": true
    },
    "permissions": {
      "path": "manifests/permissions.json",
      "required": false
    },
    "presentation": {
      "path": "manifests/presentation.json",
      "required": false
    },
    "data": {
      "path": "manifests/data.json",
      "required": false
    },
    "localization": {
      "path": "manifests/localization.json",
      "required": false
    }
  }
}
```

规则：

- `required` 为 true 的贡献清单缺失时，插件验证失败。
- 贡献清单读取仍然不能执行插件代码。
- 贡献清单必须可追踪来源文件和 hash。

### 8. 安装记录

每个已安装版本目录下必须生成安装记录：

```text
install.json
```

建议记录：

- `pluginId`
- `packageId`
- `version`
- `source`
- `packageHash`
- `contentHash`
- `installedAt`
- `installedBy`
- `pluginProfile`
- `installPath`
- `manifestHash`
- `grantedCapabilities`
- `validationResult`

安装记录用于诊断、回滚、审计和清理，不参与插件业务逻辑。

### 9. 锁定文件

每个插件 profile 需要维护锁定文件：

```text
atomui-city.plugins.lock.json
```

锁定文件记录：

- 已安装插件列表。
- 当前启用版本。
- 禁用状态。
- 包来源和 hash。
- 插件依赖解析结果。
- 授权后的能力集合。
- 上次验证结果。
- pending 操作。

Host 启动时应以锁定文件为准恢复插件启用状态，而不是只根据目录扫描结果推断。

### 10. AOT 和 Source Generator 约束

元数据系统必须优先依赖构建期生成的清单。

规则：

- 不通过运行时反射扫描插件入口。
- 不通过加载程序集读取插件 identity。
- 插件模块图、路由、权限、本地化索引等优先由 source generator 或 MSBuild task 生成。
- Native AOT 场景不支持运行时动态加载插件程序集，元数据仍可用于静态插件和资源包管理。

### 11. 诊断和测试

必须覆盖：

- 清单 schema 版本不兼容。
- `PluginId` 缺失或格式无效。
- 包内存在多个主业务程序集。
- `PluginId` 与锁定文件冲突。
- Host 版本不兼容。
- 插件 API 版本不兼容。
- 缺少 required contribution manifest。
- 能力声明被拒绝。
- 安装记录和实际文件 hash 不一致。

诊断信息必须包含 PluginId、PackageId、Version、PluginProfile、Source、Path 和失败阶段。
