# AtomUI.City.Build Output Layout 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Output Layout` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

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

## Build 输出目录设计

适用范围：`output/` 根目录、构建产物、生成产物、包、发布输出、日志和清理策略

### 1. 目标

Build 必须把框架构建输出集中到稳定目录中，避免构建产物散落在项目根目录。

设计目标：

- 本地开发、CLI 和 CI 使用同一套输出布局。
- 构建产物可清理、可诊断、可测试。
- 插件包、应用发布和 manifest 快照有固定位置。
- 路径可配置，但默认一致。

### 2. 默认根目录

默认输出根目录：

```text
output/
```

可以通过 MSBuild 属性覆盖：

```xml
<AtomUICityOutputRoot>output</AtomUICityOutputRoot>
```

规则：

- 相对路径基于 repo root 或 solution root 解析。
- CLI 调用 Build 时必须尊重同一属性。
- CI 可以覆盖到工作区 artifact 目录。
- 自定义路径必须进入 diagnostics。

### 3. 推荐布局

```text
output/
  artifacts/
    bin/
    obj/
    generated/
    manifests/
    diagnostics/
  packages/
    nuget/
    plugins/
    templates/
  publish/
    apps/
    plugins/
    resources/
  logs/
```

### 4. artifacts

`artifacts` 保存构建中间产物和可诊断快照。

| 路径 | 内容 |
|---|---|
| `artifacts/bin` | 构建输出程序集副本或统一收敛视图。 |
| `artifacts/obj` | 框架 task 中间产物。 |
| `artifacts/generated` | source generator 和 MSBuild 生成文件。 |
| `artifacts/manifests` | 最终 manifest 快照。 |
| `artifacts/diagnostics` | 结构化构建诊断。 |

项目自己的 `bin/obj` 可以保留 .NET 默认行为，但 AtomUI.City 需要复制或生成可诊断快照到 `output/artifacts`。

### 5. packages

`packages` 保存可发布包：

| 路径 | 内容 |
|---|---|
| `packages/nuget` | 框架或普通 NuGet 包。 |
| `packages/plugins` | 插件 NuGet 包。 |
| `packages/templates` | 模板包。 |

插件包必须经过 package layout validation 后进入 `packages/plugins`。

### 6. publish

`publish` 保存发布输出：

| 路径 | 内容 |
|---|---|
| `publish/apps` | 应用发布目录。 |
| `publish/plugins` | bundled/static plugin 输出。 |
| `publish/resources` | 资源包、语言包、`.locpack`。 |

发布目录不等同于运行时用户插件安装目录。用户插件安装目录由 PluginSystem 运行时管理。

### 7. logs

`logs` 保存人类可读构建日志。

结构化诊断优先写入 `artifacts/diagnostics`，`logs` 用于开发者排查。

### 8. 清理策略

建议 target：

- `CleanAtomUICityOutput`
- `CleanAtomUICityGenerated`
- `CleanAtomUICityPackages`
- `CleanAtomUICityPublish`

规则：

- 默认 clean 只清理当前项目相关产物。
- 全量清理必须显式执行。
- 清理不能删除用户插件安装目录。
- 清理不能删除 PluginSystem 运行时 cache。

### 9. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 默认输出根目录 | Unit | 未配置时解析到 `output/`。 |
| 自定义输出根目录 | Unit | 属性覆盖生效。 |
| artifacts 分类 | Unit/Build | generated、manifests、diagnostics 路径正确。 |
| packages 分类 | Build | plugin package 输出到 `packages/plugins`。 |
| publish 分类 | Build | app/resource 输出到 publish 子目录。 |
| clean | Build | 只清理目标目录，不删除运行时插件目录。 |
