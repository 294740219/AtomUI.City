# AtomUI.City.Core Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | 已文档化 | API 合同 | 现有测试文件 | 实现基线 | 产品合同测试 | 必要断言 | 实现缺口 | 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-CORE-001 | 是 | 是 | ApplicationHostBuilderTests; ApplicationHostRuntimeTests | 已实现 | 已通过 | 断言 Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-002 | 是 | 是 | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests | 已实现 | 已通过 | 断言 stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-003 | 是 | 是 | LifecycleScopeTreeTests | 已实现 | 已通过 | 断言 leaf-first、parent-child 状态、dispose 后 mutating API 失败。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-004 | 是 | 是 | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests; ApplicationHostModuleLifecycleTests | 已实现 | 已通过 | 断言依赖排序、默认 id、显式 id、模块来源、PreConfigure 顺序、配置阶段禁止解析运行时服务、配置阶段结束后拒绝继续修改服务注册。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-005 | 是 | 是 | ServiceRegistrationAttributeTests | 已实现 | 已通过 | 断言 lifetime、exposed services、AOT metadata 可读。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-006 | 是 | 是 | HostDiagnosticsTests | 已实现 | 已通过 | 断言现有 AUCHOST001/002/003 和目标失败诊断上下文。 | 已关闭 | 已实现并通过产品合同测试 |
| AUC-CORE-007 | 是 | 是 | UiDispatcherIntegrationTests | 已实现 | 已通过 | 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。 | 已关闭 | 已实现并通过产品合同测试 |

## 更新规则

- `Implementation Baseline` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为已实现。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
