# AtomUI.City.Templates Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 生成项目必须 restore、build 和 test。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 模板变量必须校验，非法值不写文件。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 输出不得包含机器绝对路径。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| dry-run 只生成 TemplatePlan，不写文件。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-TEMPLATES-001 | TemplateSmoke | ApplicationTemplateBuildSmokeTests | 断言生成、restore/build/test、命名空间、包引用、solution、Directory.Build、docs entry、无绝对路径。 | 缺少 solution、Directory.Build、docs entry 或生成项目无法 restore/build/test 必须失败。 | Completed |
| AUC-TEMPLATES-002 | TemplateSmoke | TemplatePackageLayoutTests | 断言 required files、路径规范化、重复文件、路径逃逸和 package id。 | 路径逃逸返回 `AUCTPL1001` 或参数异常；重复 normalized path 返回 `AUCTPL1002`；非法 change type 返回 `AUCTPL1003`。 | Completed |
| AUC-TEMPLATES-003 | TemplateSmoke | TemplatePackageLayoutTests | 断言变量默认值、非法值、命名空间生成和错误消息。 | 非法 identifier、保留字、空值、路径片段非法返回 validation diagnostic。 | Required |
| AUC-TEMPLATES-004 | TemplateSmoke | TemplatePackageLayoutTests | 断言单 assembly、NuGet metadata、manifest、msbuild 属性和测试项目。 | plugin id 非法、capability 变量非法、package metadata 缺失失败。 | Required |
| AUC-TEMPLATES-005 | TemplateSmoke | ApplicationTemplateBuildSmokeTests | 断言测试项目 build/test、TestLayer、Testing 引用边界和命名规则。 | 测试项目名非法、生产项目误引用 Testing、缺失 TestLayer 失败。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
