# AtomUI.City.Security Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-SECURITY-001 | Yes | Yes | AuthenticationStateTests | Baseline Exists | Required | 断言 snapshot 不可变、状态切换、订阅通知、重复设置和 logout。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-002 | Yes | Yes | AuthenticationStateTests | Baseline Exists | Required | 断言 authenticated、anonymous、claims 读取和并发 snapshot。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-003 | Yes | Yes | PermissionRegistryTests; PermissionCheckerTests | Baseline Exists | Required | 断言注册、重复、未注册、插件撤销和 checker result。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-004 | Yes | Yes | AuthorizationEvaluatorTests; AuthorizationPolicyTests | Baseline Exists | Required | 断言成功、拒绝、失败、取消、多 requirement 和 provider 异常。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-005 | Yes | Yes | RouteAuthorizationGuardTests | Baseline Exists | Required | 断言 allow、deny、redirect login、取消和 Routing 无 Security 反向依赖。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-006 | Yes | Yes | CommandAuthorizationSourceTests | Baseline Exists | Required | 断言状态变化、禁用/隐藏策略、订阅释放和权限撤销。 | Required | Ready to Start Product Implementation |
| AUC-SECURITY-007 | Yes | Yes | SecurityRegistrationTests | Baseline Exists | Required | 断言成功、失败、不可用、取消、DI 默认 provider 和 Data 集成前置条件。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
