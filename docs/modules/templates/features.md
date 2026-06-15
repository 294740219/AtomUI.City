# AtomUI.City.Templates Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-TEMPLATES-001 | Application Template | Completed | ApplicationTemplateOptions, ApplicationTemplateRenderer | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-002 | Package Layout | Completed | TemplatePlan, TemplateChange | TemplatePackageLayoutTests |
| AUC-TEMPLATES-003 | Template Variables | Completed | ApplicationTemplateOptions, TemplateRenderResult | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-004 | Plugin Template | Ready to Start Product Implementation | ApplicationTemplateRenderer, Plugin template package | TemplatePackageLayoutTests |
| AUC-TEMPLATES-005 | Test Template | Ready to Start Product Implementation | ApplicationTemplateRenderer, test project template | ApplicationTemplateBuildSmokeTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 生成项目必须 restore、build 和 test。 | 必须有实现、测试或工程门禁证据。 |
| 模板变量必须校验，非法值不写文件。 | 必须有实现、测试或工程门禁证据。 |
| 输出不得包含机器绝对路径。 | 必须有实现、测试或工程门禁证据。 |
| dry-run 只生成 TemplatePlan，不写文件。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-TEMPLATES-001 Application Template

Feature ID: `AUC-TEMPLATES-001`
Status: Completed
Goal: 生成符合 AtomUI.City 架构和工程规范的应用项目。
Public Contract: ApplicationTemplateOptions, ApplicationTemplateRenderer
Runtime / Build Behavior: 输出 solution、app project、test project、docs entry、Directory.Build 对齐项；生成后可通过本地包源 restore/build/test。
Failure Behavior: 项目名非法、目标冲突、模板资源缺失不写半成品。
Threading / Cancellation: 文件写入可取消；取消后 result 包含已写入文件清单。
Diagnostics: diagnostic 必须包含 template id、target path 和 variable。
Tests: `ApplicationTemplateBuildSmokeTests`
Required Assertions: 已断言生成、restore/build/test、命名空间、包引用、solution、Directory.Build、docs entry 和无绝对路径。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TEMPLATES-002 Package Layout

Feature ID: `AUC-TEMPLATES-002`
Status: Completed
Goal: 保证模板包内容完整且路径安全。
Public Contract: TemplatePlan, TemplateChange
Runtime / Build Behavior: 模板文件必须位于声明 root，plan 中每个 change 都有 normalized relative path；template metadata 必须提供稳定 identity 和 shortName。
Failure Behavior: 路径逃逸返回 `AUCTPL1001` 或由 `TemplateChange.Create` 拒绝，重复 normalized path 返回 `AUCTPL1002`，非法 change type 返回 `AUCTPL1003`。
Threading / Cancellation: layout 校验为纯 CPU/文件读取；可观察 token。
Diagnostics: diagnostic 必须包含 raw path、normalized path 或 change type。
Tests: `TemplatePackageLayoutTests`
Required Assertions: 已断言 required files、路径规范化、重复文件、路径逃逸和 package id。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TEMPLATES-003 Template Variables

Feature ID: `AUC-TEMPLATES-003`
Status: Completed
Goal: 定义模板变量 schema、默认值和验证规则。
Public Contract: ApplicationTemplateOptions, TemplateRenderResult
Runtime / Build Behavior: 变量包括 appName、rootNamespace、targetFramework、includeTests、useAot、useDynamicPlugins 和 includeSample；渲染前统一校验，空 rootNamespace 默认派生自 appName。
Failure Behavior: 非法 identifier、保留字、空值、路径片段非法返回 `AUCTPL0001`；框架命名空间返回 `AUCTPL0002`；AOT 与动态插件冲突返回 `AUCTPL0301`。
Threading / Cancellation: 校验无副作用；取消发生在 render 前。
Diagnostics: diagnostic 包含 variable name、raw value 和 rule。
Tests: `ApplicationTemplateBuildSmokeTests`
Required Assertions: 已断言变量默认值、非法值、命名空间生成和错误消息。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TEMPLATES-004 Plugin Template

Feature ID: `AUC-TEMPLATES-004`
Status: Ready to Start Product Implementation
Goal: 生成单 assembly 插件项目和 NuGet 打包约定。
Public Contract: ApplicationTemplateRenderer, Plugin template package
Runtime / Build Behavior: 输出插件 csproj、manifest、module、package metadata、plugin tests 和 pack properties。
Failure Behavior: plugin id 非法、capability 变量非法、package metadata 缺失失败。
Threading / Cancellation: 文件写入可取消；pack smoke 可由后续测试执行。
Diagnostics: diagnostic 必须包含 plugin id、package id 和 contribution。
Tests: `TemplatePackageLayoutTests`
Required Assertions: 断言单 assembly、NuGet metadata、manifest、msbuild 属性和测试项目。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-TEMPLATES-005 Test Template

Feature ID: `AUC-TEMPLATES-005`
Status: Ready to Start Product Implementation
Goal: 为模块、插件和应用生成符合 Testing 模块规范的测试项目。
Public Contract: ApplicationTemplateRenderer, test project template
Runtime / Build Behavior: 输出 xUnit 项目、TestLayer 标记、Testing 包引用和基础 smoke tests。
Failure Behavior: 测试项目名非法、生产项目误引用 Testing、缺失 TestLayer 失败。
Threading / Cancellation: 渲染可取消；生成后可参与 solution test。
Diagnostics: diagnostic 必须包含 test project path 和 layer。
Tests: `ApplicationTemplateBuildSmokeTests`
Required Assertions: 断言测试项目 build/test、TestLayer、Testing 引用边界和命名规则。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
