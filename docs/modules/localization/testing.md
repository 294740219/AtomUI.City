# AtomUI.City.Localization Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 语言包按当前 culture 和 fallback chain 懒加载，不在启动时全量加载。 | 必须断言未使用 culture 不加载。 |
| 每种语言自己的语言包独立缓存、独立诊断、独立撤销。 | 必须断言不同 culture 缓存隔离。 |
| 语言包可以来自 Host、模块、插件或独立 assembly，但都必须有 owner。 | 必须断言 owner revoke 后不可 lookup。 |
| 缺失 key 必须诊断并走 fallback，不允许静默返回空字符串。 | 必须断言 diagnostics 和 fallback 顺序。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-LOCALIZATION-001 | Contract | CultureStateTests | 断言默认 culture、fallback 顺序、非法 culture、fallback cycle 和重复切换。 | 非法 culture、fallback cycle、重复设置。 | Completed |
| AUC-LOCALIZATION-002 | Contract | LanguagePackageProviderTests | 断言 provider 注册、重复拒绝、取消、格式错误和 owner revoke。 | provider 格式错误、取消、重复 package、owner 已撤销。 | Completed |
| AUC-LOCALIZATION-003 | Contract | LocalizationServiceTests | 断言按需加载、并发合并、失败 fallback、不同 culture 独立缓存。 | load 失败、并发 load、取消污染缓存。 | Completed |
| AUC-LOCALIZATION-004 | Contract | LocalizationServiceTests | 断言 scope lookup、fallback、缺失 key、参数格式化和订阅更新。 | 缺失 key、格式化参数错误。 | Completed |
| AUC-LOCALIZATION-005 | Contract | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests | 断言独立 assembly、属性声明、资源读取、缺失资源和 unload owner。 | assembly load 失败、manifest 缺失、资源缺失。 | Required |
| AUC-LOCALIZATION-006 | Contract | LocalizationServiceTests | 断言 bridge 调用、局部失败、批量刷新和不依赖 Avalonia 类型。 | Presentation bridge 失败、target 局部失败。 | Required |
| AUC-LOCALIZATION-007 | Contract | LanguagePackageProviderTests | 断言撤销后不可 lookup、旧 snapshot 稳定、订阅释放和重复 revoke。 | 插件 active text binding 未释放、unload 与 lookup 并发。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
