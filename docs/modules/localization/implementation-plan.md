# AtomUI.City.Localization Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-LOCALIZATION-001 | Yes | Yes | CultureStateTests | Baseline Exists | Required | 断言默认 culture、fallback 顺序、非法 culture 和重复切换。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-002 | Yes | Yes | LanguagePackageProviderTests | Baseline Exists | Required | 断言 provider 注册、重复拒绝、取消、格式错误和 owner revoke。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-003 | Yes | Yes | LocalizationServiceTests | Baseline Exists | Required | 断言按需加载、并发合并、失败 fallback、不同 culture 独立缓存。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-004 | Yes | Yes | LocalizationServiceTests | Baseline Exists | Required | 断言 scope lookup、fallback、缺失 key、参数格式化和订阅更新。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-005 | Yes | Yes | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests | Baseline Exists | Required | 断言独立 assembly、属性声明、资源读取、缺失资源和 unload owner。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-006 | Yes | Yes | LocalizationServiceTests | Baseline Exists | Required | 断言 bridge 调用、局部失败、批量刷新和不依赖 Avalonia 类型。 | Required | Ready to Start Product Implementation |
| AUC-LOCALIZATION-007 | Yes | Yes | LanguagePackageProviderTests | Baseline Exists | Required | 断言撤销后不可 lookup、旧 snapshot 稳定、订阅释放和重复 revoke。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
