# AtomUI.City.Mvvm Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。 | Assembly 和 API 测试必须断言无 Presentation/Avalonia visual 依赖。 |
| Interaction 只表达请求，展示和 handler 注册由 Presentation 承担。 | Interaction 测试必须断言无 handler 返回失败。 |
| Command、Activation、Interaction、Validation 都必须有取消和失败结果语义。 | 必须覆盖取消、异常、重复调用和 Dispose 后行为。 |
| ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。 | activation/deactivation 测试必须覆盖 CanDeactivate 拒绝。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-MVVM-001 | Contract | ViewModelBaseTests | 断言 PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。 | Dispose 后 mutating API、重复通知、空 property name。 | Required |
| AUC-MVVM-002 | Contract | ActivationScopeTests; DeactivationTests | 断言状态机、拒绝停用、取消、异常映射和资源释放。 | 激活失败、CanDeactivate 拒绝、取消、重复 Dispose。 | Required |
| AUC-MVVM-003 | Contract | CommandTests | 断言成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。 | execute 异常、并发执行、取消、CanExecute 变更。 | Required |
| AUC-MVVM-004 | Contract | InteractionTests | 断言有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。 | 无 handler、handler 异常、取消后结果。 | Required |
| AUC-MVVM-005 | Contract | ValidationScopeTests | 断言消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。 | 未知 field、重复 message、Dispose 后更新。 | Required |
| AUC-MVVM-006 | Contract | CommandTests | 断言状态转换、取消顺序、重复终态、耗时字段和资源释放。 | 重复 Complete/Fail/Cancel、Dispose 后新操作。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
