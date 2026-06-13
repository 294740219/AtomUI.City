# AtomUI.City.Build Plugin Packaging 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Packaging` 相关实现决策，不重新定义模块边界。

## 设计决策

- 包布局必须可由测试断言。
- 路径必须使用跨平台分隔符处理。
- 安装目录不得允许路径穿越。
- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

## Public Contract

- 只允许通过 `AtomUI.City.Build` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-BUILD-001 | Output Layout | OutputLayoutTests |
| AUC-BUILD-002 | Package Metadata | PackageMetadataTests |
| AUC-BUILD-003 | Project Inventory | ProjectInventoryTests |
| AUC-BUILD-004 | Dependency Boundary | ProjectDependencyBoundaryTests |
| AUC-BUILD-005 | Source Generator Packaging | SourceGeneratorProjectStructureTests |
| AUC-BUILD-006 | Release Gates | PackagingReleaseGateTests; EngineeringGateTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时包不依赖 Build 生产程序集` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Build 插件打包设计

适用范围：插件 NuGet 包、plugin manifest、贡献清单、资源、hash、签名输入和包布局校验

### 1. 目标

Build 负责把插件项目打包为符合 PluginSystem 规范的独立 NuGet 包。

插件系统包布局见：[PluginSystem 包布局设计](../plugins/package-layout.md)。

### 2. 打包流程

```text
Build plugin project
-> Run source generators
-> Generate contribution manifests
-> Generate plugin.json
-> Validate one main assembly
-> Collect resources
-> Collect native/RID assets
-> Compute manifest hashes
-> Pack nupkg
-> Validate package layout
-> Copy to output/packages/plugins
-> Write diagnostics
```

### 3. 第一版规则

- 一个插件包一个 `PluginId`。
- 一个插件包一个主业务程序集。
- 插件包必须包含 `atomui-city/plugin.json`。
- 插件包必须包含 required contribution manifests。
- 语言包、`.locpack`、图标、样式、native asset 可以作为资源。
- 插件包不能依赖运行时目录结构补齐缺失 manifest。
- 包内容 hash 必须稳定。

### 4. 包输出

插件包输出：

```text
output/packages/plugins/
  <PackageId>.<Version>.nupkg
```

诊断输出：

```text
output/artifacts/diagnostics/plugins/
```

Manifest 快照：

```text
output/artifacts/manifests/plugins/<PluginId>/<Version>/
```

### 5. 校验

必须校验：

- `PluginId` 存在。
- `PackageId` 存在。
- 主程序集唯一。
- `plugin.json` schema。
- required manifest 存在。
- capability 声明格式。
- dependency version range。
- language package culture。
- native/RID asset 声明。
- content hash。

### 6. 本地开发安装

Build 可以提供 target：

```text
InstallAtomUICityPluginToLocalCache
```

规则：

- 只安装到 development profile。
- 不覆盖 stable profile。
- 不使用真实用户目录，除非用户显式配置。
- 安装结果必须更新 development lock file。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| plugin.json 生成 | Build test | 字段完整、schema 正确。 |
| 单主程序集 | Build test | 多主程序集失败。 |
| required manifest | Build test | 缺失失败。 |
| resource collection | Build test | language、asset、native 进入包。 |
| hash | Unit/Build | 内容变化 hash 变化。 |
| package output | Build | nupkg 进入 `output/packages/plugins`。 |
| dev install | Build | 只写入 development profile。 |
