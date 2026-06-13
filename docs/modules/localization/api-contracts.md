# AtomUI.City.Localization API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Culture | CultureState, LocalizationOptions | 当前 culture 和 fallback 链。 | 状态发布不可变。 |
| Package Provider | ILanguagePackageProvider, LanguagePackageDescriptor | 语言包来源。 | provider 必须声明 owner、culture 和 scope。 |
| Lookup | ILocalizationService, LocalizedString, LocalizedText | 文本查找和订阅更新。 | 缺失 key 诊断并 fallback。 |
| Assembly Package | AssemblyLanguagePackageProvider, LanguagePackageAttribute | 独立 assembly 语言包。 | 运行时按 culture 懒加载。 |
| Presentation Bridge | IPresentationLocalizationBridge | 通知 UI 刷新。 | Localization 不直接操作 VisualTree。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ILocalizationService.SetCultureAsync | 切换当前 culture。 | culture 不得为空；options 可指定 fallback。 | LocalizationResult。 | 非法 culture、load 部分失败按 result/diagnostics 表达。 | 必须观察 token；取消不提交新 state。 | 并发切换按最后提交或串行策略，必须文档化并测试。 |
| ILanguagePackageProvider.LoadAsync | 加载指定 culture 的语言包。 | culture、scope、owner。 | LanguagePackageLoadResult。 | 格式错误、资源缺失返回 Failed。 | 取消后不得缓存 partial package。 | 同一 package 并发 load 合并或拒绝。 |
| ILocalizationService.GetString | 查找字符串。 | key、scope、culture override、arguments。 | LocalizedString 或 LocalizationResult。 | 缺失 key 返回 fallback/key 并诊断。 | 同步读取无 token。 | 基于 immutable provider snapshot 并发安全。 |
| LocalizedText.Subscribe | 订阅 culture change 后的文本更新。 | handler 不得为 null。 | subscription handle。 | handler 失败被隔离并诊断。 | Dispose 后不再发送。 | 通知顺序跟随 culture state version。 |
| AssemblyLanguagePackageProvider.Discover | 发现 assembly 内语言包。 | assembly 和 resource filter。 | descriptors。 | manifest 缺失或重复 key 诊断。 | 发现可同步，加载异步。 | descriptor 发布后不可变。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AssemblyLanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CultureState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `FileLanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILanguagePackageProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizationDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizationService` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedText` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationLocalizationBridge` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryLocalizationDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackage` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageLoadResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LanguagePackageProviderKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticIds` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticRecord` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationErrorKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationService` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizationServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedMessage` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedResourceKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedString` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedTextChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ResourceScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
