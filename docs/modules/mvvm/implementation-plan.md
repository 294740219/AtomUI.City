# AtomUI.City.Mvvm Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-MVVM-001 | Yes | Yes | ViewModelBaseTests | Baseline Exists | Required | 断言 PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。 | Required | Ready to Start Product Implementation |
| AUC-MVVM-002 | Yes | Yes | ActivationScopeTests; DeactivationTests | Baseline Exists | Required | 断言状态机、拒绝停用、取消、异常映射和资源释放。 | Required | Ready to Start Product Implementation |
| AUC-MVVM-003 | Yes | Yes | CommandTests | Baseline Exists | Required | 断言成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。 | Required | Ready to Start Product Implementation |
| AUC-MVVM-004 | Yes | Yes | InteractionTests | Baseline Exists | Required | 断言有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。 | Required | Ready to Start Product Implementation |
| AUC-MVVM-005 | Yes | Yes | ValidationScopeTests | Baseline Exists | Required | 断言消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。 | Required | Ready to Start Product Implementation |
| AUC-MVVM-006 | Yes | Yes | CommandTests | Baseline Exists | Required | 断言状态转换、取消顺序、重复终态、耗时字段和资源释放。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
