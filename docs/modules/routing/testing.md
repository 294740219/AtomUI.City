# AtomUI.City.Routing Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Routing 只负责 Route -> ViewModel Target，不创建 View，不提交 VisualTree。 | Route graph 和 navigation 测试必须断言输出 target descriptor 后停止。 |
| RouteGraphSnapshot 发布后不可变，插件撤销只能发布新 snapshot。 | 必须断言旧 snapshot 只读、新 snapshot 不含撤销 route。 |
| 导航是事务，失败、取消或 guard 拒绝都不能提交半导航。 | 必须断言失败后 current snapshot 未变化。 |
| 所有 route discovery 必须有 AOT 友好的 manifest 或显式注册路径。 | 必须断言 generator 或显式注册路径，不依赖 runtime scan。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-ROUTING-001 | RuntimeLifecycle | RouteTemplateTests; RouteDefinitionAttributeTests | 断言合法模板、非法模板、参数边界、属性默认值和稳定排序。 | 语法错误、重复参数名、非法 catch-all 位置、未知 constraint。 | Implemented |
| AUC-ROUTING-002 | RuntimeLifecycle | RouteGraphAndMatcherTests | 断言 graph 不可变、冲突拒绝、plugin route revoke 后旧 snapshot 仍只读可用。 | 冲突、缺失父 route、重复 route id、插件撤销失败。 | Required |
| AUC-ROUTING-003 | RuntimeLifecycle | RouteGraphAndMatcherTests; RoutingParameterBoundaryTests | 断言优先级、参数转换、constraint、并发匹配和非法输入。 | 参数缺失、格式不匹配、constraint 拒绝。 | Required |
| AUC-ROUTING-004 | RuntimeLifecycle | NavigationScopeTests | 断言失败不改变 current snapshot、取消不提交、重复 dispose 幂等、并发策略稳定。 | 取消、并发策略拒绝、resolver 失败、commit 失败。 | Required |
| AUC-ROUTING-005 | RuntimeLifecycle | RouteGuardTests | 断言 enter/leave 顺序、deny、redirect、loop detection、异常映射和取消。 | guard 抛异常、redirect loop、deny。 | Required |
| AUC-ROUTING-006 | RuntimeLifecycle | RouteGraphAndMatcherTests | 断言 target descriptor 内容完整、Routing 不依赖 Presentation、失败不创建 ViewModel。 | 缺失 ViewModel target、target type 不可构造、参数无法绑定。 | Required |
| AUC-ROUTING-007 | RuntimeLifecycle | RouteGraphAndMatcherTests | 断言插件贡献、冲突隔离、卸载撤销、旧 snapshot 只读。 | 插件 route 冲突、插件卸载并发导航。 | Required |
| AUC-ROUTING-008 | RuntimeLifecycle | NavigationScopeTests | 断言 push/replace/back/forward、容量裁剪、失败不写历史和 reuse key。 | journal 容量溢出、失败导航、replace/back 边界。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
