# AtomUI.City.Presentation Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-PRESENTATION-001 | Yes | Yes | AvaloniaUiDispatcherTests; PresentationPlatformIntegrationTests | Baseline Exists | Required | 断言 UI 线程识别、后台 marshal、取消、异常映射和平台不可用。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-002 | Yes | Yes | ViewLocatorTests | Baseline Exists | Required | 断言 manifest 注册、显式覆盖、重复拒绝、插件撤销和 O(1) lookup 路径。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-003 | Yes | Yes | ViewBindingTests | Baseline Exists | Required | 断言构造参数、DataContext、失败回滚、handle dispose 和 lifecycle event。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-004 | Yes | Yes | RouteOutletTests | Baseline Exists | Required | 断言成功替换、失败回滚、取消、重复 commit、旧 view dispose 和结果状态。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-005 | Yes | Yes | VisualFeedbackTests | Baseline Exists | Required | 断言 attach/detach、focus、visibility、反馈顺序和 handler 失败隔离。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-006 | Yes | Yes | PresentationInteractionHandlerTests; ValidationVisualStateBindingTests | Baseline Exists | Required | 断言 handler 注册撤销、无 handler、验证消息变化、控件释放和取消。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-007 | Yes | Yes | PresentationLocalizationBridgeTests; PresentationResourceRegistryTests | Baseline Exists | Required | 断言 culture 切换、fallback、resource revoke、插件资源卸载和局部失败隔离。 | Required | Ready to Start Product Implementation |
| AUC-PRESENTATION-008 | Yes | Yes | ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests | Baseline Exists | Required | 断言 active view lease、卸载撤销、拒绝卸载、资源释放和重复 unload。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
