# AtomUI.City.Security Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Security 不实现登录 UI，只定义状态、权限、授权和 token contract。 | 测试必须断言无 Presentation 依赖。 |
| 认证状态以 immutable snapshot 发布，跨线程读取必须一致。 | 必须有并发读取测试。 |
| 授权评估不操作 UI、不执行导航、不访问 VisualTree。 | Route guard 测试只断言结果，不触发导航。 |
| Route、Command、Data 只通过 Security public contract 集成。 | 集成测试必须断言边界。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-SECURITY-001 | Contract | AuthenticationStateTests | 断言 snapshot 不可变、状态切换、订阅通知、重复设置和 logout。 | provider 失败、半认证状态、退出登录。 | Completed |
| AUC-SECURITY-002 | Contract | AuthenticationStateTests | 断言 authenticated、anonymous、claims 读取和并发 snapshot。 | principal 缺失、claims 格式异常。 | Completed |
| AUC-SECURITY-003 | Contract | PermissionRegistryTests; PermissionCheckerTests | 断言注册、重复、未注册、插件撤销和 checker result。 | 未注册权限、重复权限、contribution revoked 后重新注册。 | Completed |
| AUC-SECURITY-004 | Contract | AuthorizationEvaluatorTests; AuthorizationPolicyTests | 断言成功、拒绝、失败、取消、多 requirement 和 provider 异常。 | policy 缺失、requirement 失败、provider 抛异常、预取消。 | Completed |
| AUC-SECURITY-005 | Contract | RouteAuthorizationGuardTests | 断言 allow、deny、redirect login、取消和 Routing 无 Security 反向依赖。 | 无权限、需要登录、异常映射。 | Required |
| AUC-SECURITY-006 | Contract | CommandAuthorizationSourceTests | 断言状态变化、禁用/隐藏策略、订阅释放和权限撤销。 | descriptor 缺失、权限撤销、用户状态变化。 | Required |
| AUC-SECURITY-007 | Contract | SecurityRegistrationTests | 断言成功、失败、不可用、取消、DI 默认 provider 和 Data 集成前置条件。 | token 缺失、刷新失败、取消。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
