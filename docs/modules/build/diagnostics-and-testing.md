# AtomUI.City.Build Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## Build 诊断和测试设计

适用范围：构建诊断、错误码、MSBuild target test、generator/analyzer test、manifest snapshot、package layout test 和测试矩阵

### 1. 目标

Build 的失败必须可解释、可测试、可定位。构建期错误不能只表现为 MSBuild target 失败，必须有稳定 diagnostic code 和上下文。

### 2. 诊断上下文

诊断至少包含：

- diagnostic code。
- severity。
- target name。
- project path。
- item identity。
- manifest path。
- plugin id，如果适用。
- package id，如果适用。
- source file 和 location，如果适用。
- output path。
- remediation message。

### 3. 错误码建议

| Code | 含义 |
|---|---|
| `AUCBLD0001` | 输出目录无效。 |
| `AUCBLD0101` | Manifest 生成失败。 |
| `AUCBLD0102` | Manifest 校验失败。 |
| `AUCBLD0201` | 插件包布局无效。 |
| `AUCBLD0202` | 插件包多主程序集。 |
| `AUCBLD0301` | AOT 模式不支持动态插件。 |
| `AUCBLD0401` | 发布输出布局无效。 |
| `AUCGEN0001` | Source generator 输入无效。 |
| `AUCANL0001` | Analyzer 规则违反。 |

### 4. 测试工具

Testing 包应支持：

- `BuildTestHost`。
- `MsBuildProjectFixture`。
- `GeneratorTestHost`。
- `AnalyzerTestHost`。
- `ManifestAssertions`。
- `PackageLayoutAssertions`。
- `PublishLayoutAssertions`。
- `BuildDiagnosticsAssertions`。
- `IncrementalBuildDriver`。

### 5. 测试类型

| 类型 | 用途 |
|---|---|
| Unit test | 路径解析、hash、manifest merge、validation。 |
| Generator test | source generator 输入输出。 |
| Analyzer test | diagnostic id、location、severity。 |
| Build test | MSBuild target、package、publish。 |
| Snapshot test | manifest 和 generated artifact 稳定性。 |

### 6. 功能点测试门禁

Build 每个功能点必须有测试矩阵条目。

规则：

- MSBuild target 必须有 build test。
- Source generator 必须有 generator test。
- Analyzer 必须有 analyzer test。
- Manifest 必须有 validation test。
- Package layout 必须有 package test。
- AOT 诊断必须有 build/analyzer test。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| output root | Unit | 默认和覆盖路径。 |
| manifest generation | Generator/Build | 生成、校验、hash。 |
| analyzer | Analyzer | diagnostic id 和 location。 |
| plugin package | Build | 布局、单主程序集、资源。 |
| application publish | Build | 发布目录、manifest、AOT。 |
| incremental | Build | no-op 和缓存失效。 |
| diagnostics | Unit/Build | 错误码和上下文。 |

### 8. 无真实外部依赖

Build 测试不能依赖真实 NuGet feed、真实用户插件目录或真实部署平台。

需要包源时使用本地临时 feed。需要插件安装目录时使用测试临时目录。
