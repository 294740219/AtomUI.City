# AtomUI.City.Build Incremental Build 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Incremental Build` 相关实现决策，不重新定义模块边界。

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

## Build 增量构建设计

适用范围：增量生成、缓存、输入输出追踪、确定性输出和 CI 可复现性

### 1. 目标

Build 必须支持稳定增量构建，避免每次构建都重复生成所有 manifest 和包。

设计目标：

- 输入不变，输出不变。
- 输出内容 deterministic。
- manifest 和 hash 可复现。
- CI 和本地行为一致。
- 增量缓存错误时可以安全重建。

### 2. 输入追踪

输入包括：

- 源码。
- AdditionalFiles。
- MSBuild properties。
- MSBuild items。
- resource files。
- language packages。
- plugin assets。
- generator version。
- build task version。

输入变化必须触发相关输出重建。

### 3. 输出追踪

输出包括：

- generated C#。
- intermediate manifest。
- final manifest。
- plugin package。
- application manifest。
- diagnostics。

输出应带内容 hash 或输入 hash 摘要。

### 4. 确定性规则

规则：

- 不把当前时间写入核心输出。
- 不把机器绝对路径写入核心输出。
- 不使用随机顺序。
- manifest 排序稳定。
- package 内容顺序稳定。
- diagnostic 输出稳定。

### 5. 缓存失效

缓存失效条件：

- 源文件变化。
- AdditionalFiles 变化。
- MSBuild 属性变化。
- generator/task 版本变化。
- manifest schema 版本变化。
- resource hash 变化。

缓存不可信时，可以删除后完整重建。

### 6. CI 复现

CI 应能断言：

- clean build 和 incremental build 输出一致。
- 同一输入在不同机器核心 manifest hash 一致。
- 输出目录不包含机器本地路径。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| input hash | Unit | 源码和 item 变化触发重建。 |
| no-op build | Build | 未变化不重写输出。 |
| deterministic manifest | Build | clean/incremental 输出一致。 |
| cache invalidation | Build | generator 版本变化触发重建。 |
| path stability | Unit/Build | 输出不包含临时绝对路径。 |
