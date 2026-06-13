# AtomUI.City.State Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 状态写入先完成原子提交，再通知订阅者。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 默认不隐式切 UI 线程。 | 至少一个明确测试断言，不能只断言流程成功。 |
| StateSnapshot 创建后不可变。 | 至少一个明确测试断言，不能只断言流程成功。 |
| ComputedState 不能形成循环依赖。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-STATE-001 | Unit | WritableStateTests | 断言原子更新、version、通知顺序、写拒绝。 | 写拒绝、handler 失败、disposed state。 | Required |
| AUC-STATE-002 | Unit | ApplicationStateTests | 断言注册、读取、writer、not registered。 | 未注册、重复注册、写入拒绝。 | Required |
| AUC-STATE-003 | Unit | ComputedStateTests | 断言依赖失效、缓存、异常诊断。 | compute 异常、循环依赖、dispose。 | Required |
| AUC-STATE-004 | Unit | StateScopeTests; StateThreadingTests | 断言 dispose 后不通知。 | 重复释放、owner dispose、callback 失败。 | Required |
| AUC-STATE-005 | Unit | StateSnapshotTests | 断言不可变、过滤、restore diagnostics。 | 版本不兼容、policy 拒绝、restore 失败。 | Required |
| AUC-STATE-006 | Unit | StateCollectionTests | 断言 change kind。 | 重复 key、missing key、clear。 | Required |
| AUC-STATE-007 | Unit | StateDiagnosticsTests | 断言 AUCSTA001-010。 | diagnostics collector 缺失。 | Required |
| AUC-STATE-008 | Unit | StateThreadingTests | 断言不隐式 UI。 | 并发写、调度器不可用。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，`implementation-plan.md` 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
