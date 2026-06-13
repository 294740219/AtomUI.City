# AtomUI.City.Presentation Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Presentation 负责 ViewModel -> View、UI Dispatcher、Outlet 提交和 UI 运行时桥接。 | 必须通过端到端测试证明 Route target 到 outlet commit。 |
| 所有 VisualTree 修改必须在 UI dispatcher 上执行。 | 必须断言后台线程提交被 marshal 或拒绝。 |
| ViewLocator 默认使用 generated manifest 或显式注册，不依赖运行时程序集扫描作为唯一机制。 | 必须断言无注册时失败而不是 scan 兜底。 |
| 插件 View、resource dictionary、localized binding、interaction handler 必须绑定可撤销 owner。 | 必须覆盖插件 unload 撤销。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-PRESENTATION-001 | PlatformIntegration | AvaloniaUiDispatcherTests; PresentationPlatformIntegrationTests | 断言 UI 线程识别、后台 marshal、取消、异常映射和平台不可用。 | dispatcher unavailable、work exception、取消。 | Required |
| AUC-PRESENTATION-002 | PlatformIntegration | ViewLocatorTests | 断言 manifest 注册、显式覆盖、重复拒绝、插件撤销和 O(1) lookup 路径。 | 未注册、重复注册、owner revoked。 | Required |
| AUC-PRESENTATION-003 | PlatformIntegration | ViewBindingTests | 断言构造参数、DataContext、失败回滚、handle dispose 和 lifecycle event。 | 构造失败、binding 失败、取消。 | Required |
| AUC-PRESENTATION-004 | PlatformIntegration | RouteOutletTests | 断言成功替换、失败回滚、取消、重复 commit、旧 view dispose 和结果状态。 | commit 失败、dispatcher 失败、old view deactivate 拒绝。 | Required |
| AUC-PRESENTATION-005 | PlatformIntegration | VisualFeedbackTests | 断言 attach/detach、focus、visibility、反馈顺序和 handler 失败隔离。 | 未知 visual、重复 detach、反馈 handler 失败。 | Required |
| AUC-PRESENTATION-006 | PlatformIntegration | PresentationInteractionHandlerTests; ValidationVisualStateBindingTests | 断言 handler 注册撤销、无 handler、验证消息变化、控件释放和取消。 | 无 handler、重复 handler、控件已释放。 | Required |
| AUC-PRESENTATION-007 | PlatformIntegration | PresentationLocalizationBridgeTests; PresentationResourceRegistryTests | 断言 culture 切换、fallback、resource revoke、插件资源卸载和局部失败隔离。 | 语言包缺失、resource dictionary 加载失败、target 已释放。 | Required |
| AUC-PRESENTATION-008 | PlatformIntegration | ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests | 断言 active view lease、卸载撤销、拒绝卸载、资源释放和重复 unload。 | active view 拒绝关闭、资源撤销失败、handler 仍被引用。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，`implementation-plan.md` 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
