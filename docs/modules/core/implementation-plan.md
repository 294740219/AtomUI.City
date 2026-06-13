# AtomUI.City.Core Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-CORE-001 | Yes | Yes | ApplicationHostBuilderTests; ApplicationHostRuntimeTests | Implemented | Passed | 断言 Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-002 | Yes | Yes | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests | Implemented | Passed | 断言 stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-003 | Yes | Yes | LifecycleScopeTreeTests | Implemented | Passed | 断言 leaf-first、parent-child 状态、dispose 后 mutating API 失败。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-004 | Yes | Yes | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests | Implemented | Passed | 断言依赖排序、默认 id、显式 id、配置阶段禁止解析运行时服务。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-005 | Yes | Yes | ServiceRegistrationAttributeTests | Implemented | Passed | 断言 lifetime、exposed services、AOT metadata 可读。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-006 | Yes | Yes | HostDiagnosticsTests | Implemented | Passed | 断言现有 AUCHOST001/002/003 和目标失败诊断上下文。 | Closed | Implemented and Product Contract Tested |
| AUC-CORE-007 | Yes | Yes | UiDispatcherIntegrationTests | Implemented | Passed | 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。 | Closed | Implemented and Product Contract Tested |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
