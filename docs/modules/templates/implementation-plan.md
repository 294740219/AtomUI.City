# AtomUI.City.Templates Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-TEMPLATES-001 | Yes | Yes | ApplicationTemplateBuildSmokeTests | Baseline Exists | Required | 断言生成、restore/build/test、命名空间、包引用、无绝对路径。 | Required | Ready to Start Product Implementation |
| AUC-TEMPLATES-002 | Yes | Yes | TemplatePackageLayoutTests | Baseline Exists | Required | 断言 required files、路径规范化、重复文件、路径逃逸和 package id。 | Required | Ready to Start Product Implementation |
| AUC-TEMPLATES-003 | Yes | Yes | TemplatePackageLayoutTests | Baseline Exists | Required | 断言变量默认值、非法值、命名空间生成和错误消息。 | Required | Ready to Start Product Implementation |
| AUC-TEMPLATES-004 | Yes | Yes | TemplatePackageLayoutTests | Baseline Exists | Required | 断言单 assembly、NuGet metadata、manifest、msbuild 属性和测试项目。 | Required | Ready to Start Product Implementation |
| AUC-TEMPLATES-005 | Yes | Yes | ApplicationTemplateBuildSmokeTests | Baseline Exists | Required | 断言测试项目 build/test、TestLayer、Testing 引用边界和命名规则。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
