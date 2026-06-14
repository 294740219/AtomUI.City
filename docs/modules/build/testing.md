# AtomUI.City.Build Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 所有构建输出集中到 `output`。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| pack warning 必须失败。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 运行时包不得依赖 Testing 或 Roslyn。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| generator 包输出到 `analyzers/dotnet/cs`。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-BUILD-001 | Build | OutputLayoutTests | 断言 artifacts、packages、logs、test-results 都在 output 下。 | 路径为空、路径逃逸、散落 bin/obj 规则不一致失败。 | Implemented |
| AUC-BUILD-002 | Build | PackageMetadataTests | 断言 LGPL v3、repository、symbol、package id 和 dependency group。 | metadata 缺失、license 错误、pack warning 返回失败。 | Implemented |
| AUC-BUILD-003 | Build | ProjectInventoryTests | 断言 src/tests 项目被 inventory 覆盖。 | 新增项目未登记、测试项目缺失、孤儿项目失败。 | Required |
| AUC-BUILD-004 | Build | ProjectDependencyBoundaryTests | 断言 runtime 不依赖 Testing/Roslyn/test packages。 | runtime 引用 Testing、test packages、Roslyn analyzer internals 失败。 | Required |
| AUC-BUILD-005 | Build | SourceGeneratorProjectStructureTests | 断言 generator target、analyzer layout、runtime 不引用 generator。 | target 错误、analyzer 路径缺失、runtime 依赖失败。 | Required |
| AUC-BUILD-006 | Build | EngineeringGateTests; PackagingReleaseGateTests | 断言 docs、format、pack、test gate 可本地执行。 | 任一 gate 失败阻止 release。 | Required |
| AUC-BUILD-007 | Build | TestNamingConventionTests | 断言测试命名和模块对应关系。 | 测试项目命名偏离、模块缺少测试项目失败。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
