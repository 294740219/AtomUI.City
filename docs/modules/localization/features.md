# AtomUI.City.Localization Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-LOCALIZATION-001 | Culture State and Fallback | Completed | CultureState, LocalizationOptions | CultureStateTests |
| AUC-LOCALIZATION-002 | Language Package Provider | Completed | ILanguagePackageProvider, LanguagePackageDescriptor, LanguagePackageRegistry | LanguagePackageProviderTests |
| AUC-LOCALIZATION-003 | Lazy Package Loading | Completed | LocalizationService, LanguagePackageLoadResult | LocalizationServiceTests |
| AUC-LOCALIZATION-004 | Lookup and Missing Key Fallback | Completed | LocalizedString, LocalizedText, LocalizationResult | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Assembly Language Packages | Completed | AssemblyLanguagePackageProvider, LanguagePackageAttribute | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-006 | Presentation Refresh Bridge | Completed | IPresentationLocalizationBridge, LocalizedTextChangedEventArgs | LocalizationServiceTests |
| AUC-LOCALIZATION-007 | Plugin Package Revocation | Ready to Start Product Implementation | ResourceScope, LanguagePackageProviderKind | LanguagePackageProviderTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 语言包按当前 culture 和 fallback chain 懒加载，不在启动时全量加载。 | 必须有实现、测试或工程门禁证据。 |
| 每种语言自己的语言包独立缓存、独立诊断、独立撤销。 | 必须有实现、测试或工程门禁证据。 |
| 语言包可以来自 Host、模块、插件或独立 assembly，但都必须有 owner。 | 必须有实现、测试或工程门禁证据。 |
| 缺失 key 必须诊断并走 fallback，不允许静默返回空字符串。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-LOCALIZATION-001 Culture State and Fallback

Feature ID: `AUC-LOCALIZATION-001`
Status: Completed
Goal: 定义当前 culture、fallback 链和切换事务。
Public Contract: CultureState, LocalizationOptions
Runtime / Build Behavior: SetCulture 计算 fallback chain 并发布 immutable state；默认 culture、默认 UI culture 和 fallback culture 可配置；重复设置当前 culture 不重新加载 package 或递增 revision。
Failure Behavior: 非法 culture、fallback cycle、当前 culture 重复设置必须稳定返回。
Threading / Cancellation: culture 切换可以异步加载包；状态提交必须一次性发布。
Diagnostics: culture diagnostics 必须包含 requested/effective culture 和 fallback chain。
Tests: `CultureStateTests`
Required Assertions: 断言默认 culture、fallback 顺序、非法 culture、fallback cycle 和重复切换。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-002 Language Package Provider

Feature ID: `AUC-LOCALIZATION-002`
Status: Completed
Goal: 抽象文件、程序集和插件语言包来源。
Public Contract: ILanguagePackageProvider, LanguagePackageDescriptor, LanguagePackageRegistry
Runtime / Build Behavior: provider 声明 culture、scope、owner 和 lazy load 函数；registry 按 owner 管理 descriptors。
Failure Behavior: provider 格式错误、取消、重复 package、owner 已撤销必须返回稳定 Result。
Threading / Cancellation: load 支持 CancellationToken；取消后不得缓存 partial package。
Diagnostics: provider diagnostics 必须包含 provider kind、assembly/path、culture 和 scope。
Tests: `LanguagePackageProviderTests`
Required Assertions: 断言 provider 注册、重复拒绝、取消、格式错误和 owner revoke。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-003 Lazy Package Loading

Feature ID: `AUC-LOCALIZATION-003`
Status: Completed
Goal: 只按当前 culture 和 fallback chain 懒加载所需语言包。
Public Contract: LocalizationService, LanguagePackageLoadResult
Runtime / Build Behavior: 第一次 lookup 或 culture 切换触发 package load；cache key 包含 culture 和 package id；不同 culture 有独立缓存。
Failure Behavior: load 失败诊断后跳过该 provider，继续 fallback；不能阻塞整个应用启动。
Threading / Cancellation: 同一 culture/package 并发 load 合并为一个任务；失败结果不写入共享缓存。
Diagnostics: lazy-load diagnostics 必须包含 package id、culture、attempt 和 elapsed。
Tests: `LocalizationServiceTests`
Required Assertions: 断言按需加载、并发合并、失败 fallback、不同 culture 独立缓存。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-004 Lookup and Missing Key Fallback

Feature ID: `AUC-LOCALIZATION-004`
Status: Completed
Goal: 提供稳定文本查找、缺失 key 行为和订阅式 LocalizedText。
Public Contract: LocalizedString, LocalizedText, LocalizationResult
Runtime / Build Behavior: lookup 按 descriptor scope priority、current culture、fallback culture、missing marker 顺序查找；同一 scope 内保持 Host 注册顺序；LocalizedText 在 culture change 后更新。
Failure Behavior: 缺失 key 返回 missing marker 并记录诊断；格式化参数错误返回 raw template 并记录格式化诊断。
Threading / Cancellation: lookup 允许并发读取；LocalizedText 更新按 culture revision 发布，handler 失败被隔离。
Diagnostics: missing-key diagnostics 必须包含 key 和 culture；format diagnostics 必须包含 key、culture 和 error kind。
Tests: `LocalizationServiceTests`
Required Assertions: 断言 scope lookup、fallback、缺失 key、参数格式化和订阅更新。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-005 Assembly Language Packages

Feature ID: `AUC-LOCALIZATION-005`
Status: Completed
Goal: 支持语言包放在独立 assembly 并运行时加载。
Public Contract: AssemblyLanguagePackageProvider, LanguagePackageAttribute, LocalizedResourceAttribute
Runtime / Build Behavior: assembly provider 通过 `Discover(Assembly)` 读取 `LanguagePackageAttribute` 并生成 assembly descriptor；descriptor 记录 assembly path、resource base name、fallback culture、version、checksum 和 contribution id，随后按 culture 加载 embedded locpack。
Failure Behavior: assembly load 失败、资源缺失和 locpack 格式错误只影响该 package；owner revoke 后相同 owner 不可重新注册已发现 descriptor。
Threading / Cancellation: assembly load 可以在后台线程；加载完成后通过 service 发布。
Diagnostics: assembly package diagnostics 必须包含 assembly name、resource name 和 culture。
Tests: `LanguagePackageProviderTests; LocalizationDeclarationAttributeTests`
Required Assertions: 断言独立 assembly、属性声明、资源读取、缺失资源和 unload owner。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-006 Presentation Refresh Bridge

Feature ID: `AUC-LOCALIZATION-006`
Status: Completed
Goal: 把 culture 变化通知 Presentation 刷新 UI 文本、方向和资源。
Public Contract: IPresentationLocalizationBridge, LocalizedTextChangedEventArgs
Runtime / Build Behavior: Localization 在语言包加载成功后提交 `CultureState`，以包含 loaded package id 的 batch state 调用 Presentation bridge，并随后刷新已注册 `ILocalizedText`。
Failure Behavior: Presentation bridge 失败不能回滚 culture state；失败 result 返回给调用方，同时记录 `AtomUiApplyFailed`，本地文本刷新继续执行。
Threading / Cancellation: bridge 调用必须支持取消和批量刷新；Localization contract 不引用 Avalonia 类型，UI work 由 Presentation dispatcher 处理。
Diagnostics: bridge diagnostics 必须包含 culture 和 error kind；Presentation 侧 target count 和 failed target 由 bridge/applier 诊断承接。
Tests: `LocalizationServiceTests`
Required Assertions: 断言 bridge 调用、局部失败、批量刷新和不依赖 Avalonia 类型。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-LOCALIZATION-007 Plugin Package Revocation

Feature ID: `AUC-LOCALIZATION-007`
Status: Ready to Start Product Implementation
Goal: 插件卸载时撤销语言包、文本订阅和 provider 缓存。
Public Contract: ResourceScope, LanguagePackageProviderKind
Runtime / Build Behavior: 插件 provider 绑定 plugin owner；unload 后 lookup 不再返回插件资源。
Failure Behavior: 插件 active text binding 未释放时必须由 Presentation 协调撤销。
Threading / Cancellation: unload 与 lookup 并发时，lookup 使用开始时 snapshot，后续 lookup 使用新 provider set。
Diagnostics: plugin localization diagnostics 必须包含 plugin id、scope 和 revoked package count。
Tests: `LanguagePackageProviderTests`
Required Assertions: 断言撤销后不可 lookup、旧 snapshot 稳定、订阅释放和重复 revoke。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
