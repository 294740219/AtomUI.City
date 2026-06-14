# 模块文档设计规范

版本：v0.1
状态：强制执行
适用范围：AtomUI.City 所有模块设计文档、功能规格、API 合同、测试矩阵和后续文档重写

## 1. 目标

AtomUI.City 的模块文档是产品级实现合同，不是介绍材料。

模块文档必须能指导开发者完成以下工作：

- 设计公共 API。
- 判断模块边界。
- 实现核心对象和状态机。
- 处理生命周期、线程、取消、释放和插件卸载。
- 定义错误行为和诊断码。
- 编写单元测试、集成测试和发布门禁。
- 判断兼容性和破坏性变更。

文档质量不按篇幅衡量，按可执行性衡量。能够指导实现、测试和维护的内容必须写清楚；对模块不适用的内容不做机械堆砌。

## 2. 基本原则

- 文档必须围绕实际开发决策编写。
- 简单模块可以写紧凑，核心模块必须写深。
- 任何 public API、生命周期、线程、插件、持久化、source generator、manifest、配置或兼容性行为都必须有文档合同。
- 文档必须明确模块负责什么，也必须明确模块不负责什么。
- 文档中的功能点必须能追踪到测试。
- 文档和实现不一致时，必须先修正文档并确认，再继续实现。
- 文档不追求统一模板长度，追求决策完整。

## 3. 模块分级

模块文档按风险和复杂度分为三档。分级由模块性质决定，不由当前代码量决定。

### Level 1：简单支撑模块

适用条件：

- API 数量少。
- 没有复杂生命周期。
- 不管理线程调度。
- 不参与插件卸载。
- 不定义持久化格式或兼容 manifest。
- 不直接影响应用运行时主链路。

示例：

- 小型测试辅助类型。
- 小型构建辅助约定。
- 简单纯函数工具集合。

最小文档包：

```text
overview.md
design.md
testing.md
```

`design.md` 可以合并 API、配置、诊断和兼容说明。只要内容足以指导实现和测试，不要求拆成多个文件。

### Level 2：标准框架模块

适用条件：

- 有公共 API。
- 有 DI 注册。
- 有配置项。
- 有错误处理或诊断。
- 与其他模块存在集成关系。
- 不直接控制 Host 主生命周期或插件动态卸载。

示例：

- MVVM。
- Security。
- Localization。
- CLI。
- Templates。
- Testing 中的常规工具。

推荐文档包：

```text
overview.md
architecture.md
features.md
api-contracts.md
testing.md
diagnostics.md
integration.md
```

如果模块没有独立诊断体系，`diagnostics.md` 可以合并进 `testing.md`，但必须在文档中说明诊断不适用或由其他模块提供。

### Level 3：核心运行时模块

适用条件：

- 影响应用生命周期。
- 影响线程或 UI 调度。
- 参与插件加载、停用或卸载。
- 管理状态、路由、事件、数据请求或运行时资源。
- 定义持久化、manifest、source generator 输出或跨模块 contract。
- 存在明显兼容性承诺。

示例：

- Core / Hosting / Lifecycle / Modularity。
- PluginSystem。
- EventBus。
- State。
- Routing。
- Presentation。
- Data。
- Build / Generators。

推荐文档包：

```text
overview.md
architecture.md
features.md
api-contracts.md
lifecycle.md
threading.md
diagnostics.md
testing.md
compatibility.md
integration.md
```

Level 3 也不允许堆砌无效内容。每篇文档都必须围绕实现决策、失败行为、测试证明和兼容性边界展开。

## 4. 分级判定规则

出现以下任一情况，相关模块或功能点必须按 Level 3 深度写文档：

- 新增或修改 public API。
- 新增或修改生命周期阶段。
- 涉及 UI 线程、后台线程、调度器、取消或并发。
- 涉及插件安装、加载、启用、停用、卸载或贡献撤销。
- 涉及状态快照、持久化、恢复或版本迁移。
- 涉及路由匹配、导航、Outlet 提交或 ViewModel/View 解析。
- 涉及 EventBus 跨模块或跨插件 contract。
- 涉及 Data 请求管线、连接生命周期、重试、缓存或认证。
- 涉及 source generator、analyzer、manifest 或 MSBuild target。
- 涉及向后兼容、废弃、迁移或发布门禁。

如果模块整体是 Level 2，但某个功能点命中上述条件，该功能点必须使用高风险功能规格。

## 5. overview.md 要求

`overview.md` 是模块章程，必须回答模块边界问题。

必须包含：

- 模块定位。
- 模块目标。
- 明确非目标。
- 使用者画像。
- 能力清单。
- 禁止能力清单。
- 依赖模块。
- 禁止依赖模块。
- 与 Host 的关系。
- 与 PluginSystem 的关系，如果适用。
- 与 Testing 的关系。
- 当前成熟度状态。

成熟度状态固定为：

```text
Designed
Partially Implemented
Implemented
Verified
Deprecated
```

`overview.md` 不写大量实现细节。实现细节进入 architecture、features、api-contracts 或对应专题文档。

## 6. architecture.md 要求

`architecture.md` 是模块架构设计，必须能指导核心实现。

Level 2 模块至少说明：

- 核心概念。
- 主要组件职责。
- 组件间调用关系。
- DI 注册方式。
- 配置入口。
- 错误处理策略。
- 与其他模块的集成关系。

Level 3 模块还必须说明：

- 运行时对象模型。
- 关键对象所有权。
- 创建、持有和释放关系。
- 主流程时序。
- 失败路径时序。
- 状态机。
- 扩展点模型。
- 线程模型入口。
- 插件边界。
- AOT / trimming / source generator 约束。
- 性能、内存和资源释放约束。

核心问题必须明确：

```text
谁创建对象？
谁持有对象？
谁释放对象？
失败后状态是什么？
重复调用如何处理？
并发调用如何处理？
插件卸载时如何撤销？
Host shutdown 时如何清理？
```

对模块不适用的问题可以不展开，但必须能在文档中看出不适用的原因。

## 7. features.md 要求

`features.md` 是功能点规格表。每个可交付功能点必须有稳定 Feature ID。

Feature ID 格式：

```text
AUC-<MODULE>-<NUMBER>
```

示例：

```text
AUC-STATE-001
AUC-ROUTING-004
AUC-PLUGIN-012
```

### 普通功能规格

普通功能点使用紧凑规格：

```text
Feature ID:
Feature Name:
Status:
Goal:
User Scenario:
Scope:
Public Contract:
Core Behavior:
Failure Behavior:
Tests:
Acceptance Criteria:
```

### 高风险功能规格

命中分级判定规则的功能点必须使用高风险规格：

```text
Feature ID:
Feature Name:
Status:
Goal:
User Scenario:
Scope:
Non-goals:
Public Contract:
Runtime Model:
Lifecycle:
Threading:
Plugin Boundary:
Configuration:
Failure Modes:
Diagnostics:
AOT / Source Generation:
Compatibility:
Tests:
Acceptance Criteria:
```

规则：

- 没有 Feature ID，不允许实现。
- 没有 public contract 或 internal contract，不允许实现。
- 没有失败行为，不允许实现。
- 没有测试条目，不允许标记完成。
- Feature 状态必须与全局 1.0 进度文档一致。

## 8. api-contracts.md 要求

`api-contracts.md` 不是 public 类型清单，而是 API 行为合同。

每个 public 类型至少说明：

```text
Type:
Namespace:
Assembly:
Stability:
Purpose:
Owner:
Lifetime:
DI Lifetime:
Thread Safety:
Disposal:
Breaking Change Rules:
Tests:
```

每个关键 public 方法至少说明：

```text
Method:
Purpose:
Parameters:
Return:
Nullability:
Cancellation:
Exceptions or Result:
Idempotency:
Concurrency:
Side Effects:
Diagnostics:
Tests:
```

必须明确：

- null 如何处理。
- cancellation 何时生效。
- 错误是抛异常、返回 Result，还是写入诊断。
- 是否线程安全。
- 是否允许重复调用。
- Dispose 后调用的行为。
- 插件卸载后对象是否仍可访问。
- 哪些改动属于 breaking change。

简单模块可以把 API 合同合并进 `design.md`。合并后仍必须覆盖上述关键行为。

## 9. lifecycle.md 要求

涉及生命周期的模块必须写 `lifecycle.md`。Level 3 模块必须写。

必须包含：

- 创建阶段。
- 配置阶段。
- 注册阶段。
- 启动阶段。
- 运行阶段。
- 停止阶段。
- 释放阶段。
- 异常中断行为。
- Host shutdown 行为。
- 插件加载行为，如果适用。
- 插件卸载行为，如果适用。

状态机必须写清：

- 状态名称。
- 进入条件。
- 离开条件。
- 允许操作。
- 禁止操作。
- 非法操作的错误行为。

常用基础状态：

```text
Created
Configured
Registered
Starting
Running
Stopping
Stopped
Disposed
Faulted
```

模块可以扩展状态，但不能只写“支持生命周期”而没有状态转换规则。

## 10. threading.md 要求

涉及桌面运行时、状态、事件、路由、Presentation、Data 或插件后台任务的模块必须写线程文档。

必须说明：

- 哪些 API 只能 UI 线程调用。
- 哪些 API 可以后台线程调用。
- 哪些对象线程安全。
- 哪些对象不保证线程安全。
- UI Dispatcher 如何接入。
- 后台任务如何取消。
- 状态变更如何派发。
- 事件如何派发。
- 插件卸载时如何停止后台任务。
- 并发冲突如何解决。
- 如何避免死锁。

全局规则：

```text
默认不隐式切线程。
跨线程行为必须显式声明。
UI 更新必须经过 Presentation dispatcher。
生命周期停止时必须取消未完成任务。
```

如果模块无线程语义，可以在 design 或 architecture 中说明不适用，不要求单独文件。

## 11. configuration.md 要求

有配置行为的模块必须说明配置合同。配置简单时可以合并进 architecture 或 design。

每个配置项至少说明：

```text
Name:
Type:
Default:
Source:
Scope:
Reload Behavior:
Validation:
Failure Behavior:
Compatibility:
```

配置来源必须明确：

- appsettings。
- environment。
- command line。
- module pre-configure。
- module configure。
- plugin manifest。
- generated manifest。
- MSBuild property。

必须说明配置优先级和校验失败行为。

## 12. diagnostics.md 要求

有错误码、诊断事件或可观测行为的模块必须维护诊断合同。

诊断码表格式：

| Code | Severity | Trigger | Message Contract | Context | Recovery | Tests |
|---|---|---|---|---|---|---|

规则：

- 诊断码稳定。
- 诊断码不能复用。
- message 可以优化，但 code 含义不能漂移。
- 每个重要诊断码必须有测试断言。
- 诊断必须包含足够定位问题的上下文。

常见上下文字段：

```text
module
pluginId
routeId
stateKey
eventType
operationId
scopeId
assembly
path
```

如果模块没有自己的诊断码，必须说明错误如何上报给上层模块或 Host diagnostics。

## 13. testing.md 要求

每个模块必须有测试矩阵。简单模块可以放在 `testing.md`，也可以合并进 `design.md`。

测试矩阵格式：

| Feature ID | Test Type | Test File | Required Cases | Diagnostics Asserted | Status |
|---|---|---|---|---|---|

测试类型固定为：

```text
Unit
Contract
FrameworkIntegration
RuntimeLifecycle
PluginLifecycle
PlatformIntegration
Generator
Analyzer
Build
TemplateSmoke
Dogfood
```

强制规则：

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 生命周期功能必须有 RuntimeLifecycle 测试。
- 插件相关功能必须有 PluginLifecycle 测试。
- UI/Avalonia 接入必须有 PlatformIntegration 测试。
- Source Generator 必须有 Generator 测试。
- Build、CLI、Templates 必须有 smoke。
- 集成测试不能替代单元测试。
- 诊断码必须有断言。
- Dispose、unload、cancellation 必须有断言。

无法单元测试的功能点必须写明原因，并提供替代测试类型。

## 14. compatibility.md 要求

涉及 public API、配置、manifest、snapshot、plugin contract、generated output 或包发布的模块必须说明兼容性。

必须包含：

- public API 稳定性。
- experimental API 标记。
- breaking change 判定。
- 配置兼容策略。
- manifest 兼容策略，如果适用。
- snapshot 兼容策略，如果适用。
- plugin contract 兼容策略，如果适用。
- source generator 输出兼容策略，如果适用。
- 数据迁移策略，如果适用。
- 废弃策略。
- 版本升级策略。

废弃 API 必须说明：

```text
Deprecated Since:
Replacement:
Removal Earliest Version:
Migration:
Analyzer Diagnostic:
```

如果模块没有独立兼容面，可以在 architecture 或 design 中说明由包级兼容策略覆盖。

## 15. integration.md 要求

存在跨模块交互的模块必须说明集成合同。

每个集成点至少说明：

```text
Provider Module:
Consumer Module:
Contract:
Direction:
Lifecycle:
Threading:
Failure Behavior:
Tests:
```

重点覆盖：

- Core / Hosting。
- Lifecycle。
- Modularity。
- PluginSystem。
- EventBus。
- State。
- Routing。
- Presentation。
- Security。
- Data。
- Localization。
- Testing。
- Build / Generators / CLI / Templates。

集成文档必须说明依赖方向，不能只说明“集成某模块”。

## 16. 全局进度跟踪要求

模块实现路线必须能追踪 Feature ID，但 1.0 发布完成状态只能在 [全局 1.0 进度](../superpowers/plans/2026-06-11-development-tracking-plan.md) 中维护。

全局进度文档必须覆盖：

- 每个模块的 Feature ID。
- 每个 Feature ID 的 1.0 完成 checkbox。
- 任务完成所需的测试、文档、诊断、兼容性和发布门禁证据。
- 最终 Release Gate。

模块文档可以说明设计、API contract、测试矩阵和实现证据，但不能作为完成度来源。模块目录不再维护独立 `implementation-plan.md`。

状态固定为：

```text
Not Started
In Design
Ready for Implementation
In Implementation
Implemented
Verified
Blocked
Deprecated
```

## 17. 产品级实现合同硬性标准

模块文档必须达到“工程师可以据此实现产品级框架”的标准。以下内容是硬性要求，不是建议。

### 17.1 必须能直接指导实现

每个模块必须明确：

- 核心不变量：实现过程中任何时候都不能破坏的条件。
- 对象所有权：谁创建、谁持有、谁释放、释放失败如何处理。
- 状态机：状态名称、进入条件、离开条件、允许操作和非法操作结果。
- API 行为：参数、返回值、异常、Result、取消、幂等、并发和 Dispose 后行为。
- 失败矩阵：每类失败的触发条件、对外结果、诊断码和恢复策略。
- 线程矩阵：哪些 API 只允许 UI 线程、哪些允许后台线程、哪些对象线程安全。
- 插件边界：插件贡献如何进入、如何撤销、卸载失败如何隔离。
- AOT 策略：哪些发现行为由 source generator 或 manifest 完成，哪些反射被禁止。
- 性能边界：热路径、缓存、snapshot、队列、连接、生成输出的规模和释放要求。
- 测试门禁：每个 Feature ID 对应的测试类型、测试文件、必须断言的行为。

如果文档只写“支持某能力”，但不能回答上面任一相关问题，该文档不允许作为实现依据。

### 17.2 模块特有硬约束

每个模块的 `overview.md` 和 `architecture.md` 必须包含模块特有硬约束，不能只复制通用模板。硬约束必须使用可执行语言，例如：

```text
RouteGraph 发布后不可变；插件路由卸载只能通过生成新的 RouteGraphSnapshot 生效。
```

不合格写法：

```text
路由系统需要稳定可靠。
```

### 17.3 不允许的文档形态

以下文档形态不合格：

- 只有概念说明，没有 API 合同。
- 只有流程图，没有失败路径。
- 只有测试文件名，没有必须断言的行为。
- 只有模块职责，没有明确非职责和禁止依赖。
- 只有“未来可扩展”，没有扩展点 owner、生命周期和兼容规则。
- 只有“线程安全”，没有线程矩阵和并发冲突策略。
- 只有“插件支持”，没有加载、启用、停用、卸载、贡献撤销和诊断。
- 只有“AOT 友好”，没有 source generator、manifest 或禁止反射规则。

### 17.4 产品级重写验收

模块文档重写完成必须满足：

- 工程师能根据文档写出 public API 骨架。
- 工程师能根据文档写出核心状态机和生命周期处理。
- 工程师能根据文档写出失败路径和诊断码。
- 工程师能根据文档写出单元测试、contract test 和必要集成测试。
- 工程师能判断哪些实现会破坏插件卸载、AOT、线程模型或兼容性。
- reviewer 能根据文档拒绝不合格实现。

## 18. 文档完成门禁

模块进入实现前，必须满足对应 Level 的文档要求。

Level 1 完成标准：

- overview 完成。
- design 覆盖核心 API、行为、失败模式和适用边界。
- testing 覆盖所有 Feature ID。

Level 2 完成标准：

- overview 完成。
- architecture 或 design 能指导实现。
- features 有完整 Feature ID。
- API 合同覆盖 public API。
- testing 有测试矩阵。
- diagnostics 和 integration 根据模块实际情况覆盖。

Level 3 完成标准：

- overview 完成。
- architecture 覆盖运行时对象模型。
- features 覆盖所有功能点。
- api-contracts 覆盖 public API。
- lifecycle 有状态机。
- threading 有线程规则。
- diagnostics 有诊断合同。
- testing 有测试矩阵。
- compatibility 覆盖兼容面。
- integration 覆盖跨模块合同。

## 19. 功能完成门禁

功能点只有满足以下条件，才能标记完成：

- Feature ID 存在。
- API contract 存在，或明确为 internal contract。
- 测试矩阵存在。
- 单元测试或 contract test 存在。
- 诊断测试存在，如果适用。
- 生命周期测试存在，如果适用。
- 插件测试存在，如果适用。
- 线程测试存在，如果适用。
- 文档与实现一致。
- public API review 通过，如果涉及 public API。

## 20. 不合格文档示例

以下写法不合格：

```text
支持插件卸载。
提供线程安全能力。
支持状态快照。
提供诊断能力。
支持 AOT。
```

原因是它们没有回答实现问题。

合格写法必须至少补足关键决策：

```text
插件卸载由 PluginRuntime.StopAsync 触发。
卸载前必须撤销 ContributionLease、释放 EventBus subscription、取消插件 OperationScope。
如果 AssemblyLoadContext 在指定重试次数内不能释放，插件进入 UnloadPending。
诊断码 AUCPLG0801 必须包含 pluginId、version、remainingReferences 和 operationId。
测试必须断言 Active -> Deactivating -> Inactive -> Unloading -> Unloaded，以及 UnloadPending 失败路径。
```

## 21. 重写现有模块文档流程

重写模块文档按以下流程执行：

```text
确定模块 Level
-> 盘点现有文档
-> 盘点已有代码和测试
-> 定义 Feature ID
-> 补齐 architecture / API / lifecycle / threading / diagnostics / testing / compatibility
-> 检查文档是否能指导实现
-> 等待确认
-> 再进入代码调整或测试补齐
```

重写顺序建议：

1. Core：Hosting / Lifecycle / Modularity / DI / Configuration / Threading。
2. PluginSystem。
3. EventBus。
4. State。
5. Routing。
6. Presentation。
7. MVVM。
8. Data。
9. Security。
10. Localization。
11. Build / Generators。
12. CLI。
13. Templates。
14. Testing。

该顺序先稳定框架底座，再稳定开发者日常编程模型和工程化入口。

## 22. 最终判断标准

一份模块文档合格，不是因为它完整覆盖所有模板，而是因为它能让开发者在不猜测的情况下完成以下判断：

- 该不该在这个模块实现某能力。
- 应该暴露什么 API。
- 不应该暴露什么 API。
- 对象如何创建、持有和释放。
- 生命周期和线程边界是什么。
- 插件加载和卸载是否安全。
- 失败如何表达。
- 诊断如何定位。
- 测试如何证明。
- 哪些行为对未来版本保持兼容。

如果文档不能回答这些问题，就不能作为产品级实现依据。
