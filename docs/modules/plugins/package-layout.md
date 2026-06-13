# AtomUI.City.PluginSystem Package Layout 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Package Layout` 相关实现决策，不重新定义模块边界。

## 设计决策

- 包布局必须可由测试断言。
- 路径必须使用跨平台分隔符处理。
- 安装目录不得允许路径穿越。
- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

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

## PluginSystem 包布局设计

适用范围：插件 NuGet 包内容、安装后目录、主程序集约束和资源布局

### 1. 目标

插件包布局需要同时服务开发、发布、安装、加载、卸载和诊断。

设计目标：

- 一个插件优先对应一个 NuGet 包。
- 一个插件包只包含一个主业务程序集。
- 插件运行目录可以脱离 NuGet 全局缓存独立工作。
- 语言包、资源、native 资产和贡献清单位置稳定。
- 目录结构支持运行时更新和回滚。

### 2. NuGet 包布局

推荐包布局：

```text
<PackageId>.<Version>.nupkg
  lib/net10.0/<MainPluginAssembly>.dll
  lib/net10.0/<MainPluginAssembly>.deps.json
  atomui-city/plugin.json
  atomui-city/manifests/plugin.manifest.json
  atomui-city/manifests/modules.json
  atomui-city/manifests/routes.json
  atomui-city/manifests/permissions.json
  atomui-city/manifests/presentation.json
  atomui-city/manifests/data.json
  atomui-city/manifests/localization.json
  atomui-city/locales/zh-CN/<Plugin>.resources.dll
  atomui-city/locales/en-US/<Plugin>.resources.dll
  atomui-city/locales/zh-CN/<Plugin>.locpack
  atomui-city/assets/icon.png
  atomui-city/assets/styles/
  runtimes/<rid>/native/
```

规则：

- `lib/<tfm>` 下第一版只允许一个主业务程序集。
- 插件私有依赖可以随包携带，但必须能被依赖解析文档定义的规则识别。
- `atomui-city/plugin.json` 必须存在。
- `atomui-city/manifests` 保存构建期生成的贡献索引。
- 本地化资源和普通资产不能混放在程序集目录下。

### 3. 主程序集约束

第一版约束：

- 一个插件包只允许一个主业务程序集。
- 主程序集内可以包含多个模块。
- 多个主业务程序集应拆成多个插件包。
- 插件依赖其他插件时，通过 `PluginId` 和版本范围表达，不通过同包多程序集表达。

这样可以降低加载上下文、卸载、诊断和发布复杂度。

### 4. 安装后布局

插件安装后布局：

```text
plugins/
  installed/
    <plugin-id>/
      <version>/
        root/
          lib/net10.0/<MainPluginAssembly>.dll
          atomui-city/plugin.json
          atomui-city/manifests/
          atomui-city/locales/
          atomui-city/assets/
          runtimes/
        install.json
```

`root` 是插件运行根目录。

规则：

- `root` 内容来自验证后的包解压结果。
- Host 只从 `root` 加载插件程序集和资源。
- `install.json` 不放在 `root` 内，避免被插件当作自身资源写入。
- 安装目录完成后不可变。

### 5. 资源布局

推荐资源类型：

| 类型 | 位置 |
|---|---|
| 插件清单 | `atomui-city/plugin.json` |
| 贡献清单 | `atomui-city/manifests/*.json` |
| 本地化程序集 | `atomui-city/locales/<culture>/*.resources.dll` |
| AOT 本地化包 | `atomui-city/locales/<culture>/*.locpack` |
| 图标 | `atomui-city/assets/` |
| 样式资源 | `atomui-city/assets/styles/` |
| native 资产 | `runtimes/<rid>/native/` |

资源必须可按插件、版本、culture 和 RID 定位。

### 6. 包内容 hash

插件包必须计算：

- package hash：`.nupkg` 原始文件 hash。
- content hash：解压后参与运行的内容 hash。
- manifest hash：`plugin.json` hash。
- contribution manifest hash：每个贡献清单 hash。

规则：

- hash 写入 `install.json` 和锁定文件。
- 构建时间戳不应影响核心 content hash。
- 验证阶段发现 hash 不匹配必须拒绝安装或加载。

### 7. 非目标

第一版不支持：

- 一个 NuGet 包包含多个独立插件。
- 一个插件包含多个主业务程序集。
- 从 NuGet 全局缓存直接作为运行时根目录加载。
- 热替换正在运行的程序集文件。

### 8. 测试要求

必须覆盖：

- 标准包布局校验。
- 缺少 `plugin.json`。
- 多主程序集拒绝。
- 资源目录缺失但非必填。
- required manifest 缺失。
- hash 稳定性。
- 安装后目录与包内容一致。
