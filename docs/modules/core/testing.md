# AtomUI.City.Core Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。 | 至少一个明确测试断言，不能只断言流程成功。 |
| ApplicationHostBuilder Build 后必须冻结服务注册入口。 | 至少一个明确测试断言，不能只断言流程成功。 |
| LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。 | 至少一个明确测试断言，不能只断言流程成功。 |
| StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则，Stopped 后再次 Start 必须失败。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。 | 至少一个明确测试断言，不能只断言流程成功。 |
| IUiDispatcher 只定义抽象，Core 不提交真实 UI work。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-CORE-001 | RuntimeLifecycle | ApplicationHostBuilderTests; ApplicationHostRuntimeTests | 断言 Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。 | Build 后继续注册失败；重复 Build 行为明确；无效 options 抛异常。 | Required |
| AUC-CORE-002 | RuntimeLifecycle | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests | 断言 stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。 | middleware 异常进入 Faulted；取消不跳过 cleanup；重复 Stop 幂等；Stopped 后再次 Start 抛 InvalidOperationException。 | Required |
| AUC-CORE-003 | RuntimeLifecycle | LifecycleScopeTreeTests | 断言 leaf-first、parent-child 状态、dispose 后 mutating API 失败。 | parent disposed 后禁止创建 child；child 释放失败诊断；重复释放幂等。 | Required |
| AUC-CORE-004 | Unit | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests | 断言依赖排序、默认 id、显式 id、配置阶段禁止解析运行时服务。 | 循环依赖、缺失依赖、重复 id 失败；默认 id 使用类型全名。 | Required |
| AUC-CORE-005 | RuntimeLifecycle | ServiceRegistrationAttributeTests | 断言 lifetime、exposed services、AOT metadata 可读。 | 冲突 lifetime 或服务类型无效必须失败或诊断。 | Required |
| AUC-CORE-006 | RuntimeLifecycle | HostDiagnosticsTests | 断言现有 AUCHOST001/002/003 和目标失败诊断上下文。 | 诊断 collector 不可用不能中断 Host；记录必须可 snapshot。 | Required |
| AUC-CORE-007 | RuntimeLifecycle | UiDispatcherIntegrationTests | 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。 | 默认 dispatcher 不可用；Presentation 必须替换真实实现。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，`implementation-plan.md` 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
