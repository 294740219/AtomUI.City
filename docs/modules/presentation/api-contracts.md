# AtomUI.City.Presentation API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Dispatcher | AvaloniaUiDispatcher, IUiDispatcher | UI 线程调度桥接。 | 所有 VisualTree 修改都通过 dispatcher。 |
| View Resolution | ViewRegistry, IViewLocator, ViewFactory | ViewModel -> View 解析和创建。 | 优先 manifest 或显式注册，失败不反射兜底。 |
| Outlet | IRouteOutlet, RouteOutlet | 提交 View 到 UI 容器。 | commit 事务失败不替换旧 visual。 |
| Feedback | VisualLifecycleHub, UiStateFeedbackPolicy | VisualTree 变化反馈。 | 反馈失败不得破坏 VisualTree。 |
| Plugin Resources | PresentationResourceRegistry, ActivePluginViewRegistry | 插件 UI 资源和 active view lease。 | 插件卸载必须可撤销或阻止。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| AvaloniaUiDispatcher.InvokeAsync | 在 UI 线程执行 work。 | delegate 不得为 null。 | work result。 | dispatcher unavailable 映射 `PresentationError.DispatcherUnavailable`；work exception 原样传播并记录诊断。 | 取消后不得执行 work；非用户取消的 dispatcher shutdown 映射为 unavailable。 | UI work 串行，允许后台并发排队。 |
| AvaloniaUiDispatcher.PostAsync | 投递异步 UI work。 | delegate 不得为 null。 | 投递完成 task。 | dispatcher unavailable 映射 `PresentationError.DispatcherUnavailable`；work exception 原样传播并记录诊断。 | 取消后不得执行 queued work item。 | 后台调用 marshal 到 dispatcher 线程执行。 |
| ViewRegistry.RegisterManifest | 从 generated manifest 或显式 descriptor list 注册 ViewDescriptor。 | descriptors 不得为 null；同一 manifest 内 key 不得重复。 | void。 | 重复注册抛 `PresentationError.DuplicateView`，失败不产生部分注册；`ViewRegistrationOptions.ReplaceExisting` 可显式覆盖。 | 同步 API 无 token。 | 注册和撤销串行。 |
| IViewLocator.Locate / TryLocate | 解析 ViewModel 对应 ViewDescriptor。 | ViewModel type、view key；`ViewLookupRequest` 可携带 route id 和 owner。 | ViewDescriptor 或失败结果。 | 未注册、重复、owner revoked 返回失败，不 fallback 到反射扫描或 assignable type 扫描。 | 同步 lookup 无 token。 | registry 读并发安全，lookup 使用精确 dictionary key。 |
| ViewFactory.Create | 创建 View 并准备 DataContext。 | ViewDescriptor、ViewModel、factory context。 | BoundViewHandle。 | 构造失败释放中间资源。 | UI 创建必须支持取消前检查。 | 每次创建独立 handle。 |
| IRouteOutlet.CommitAsync | 把 bound view 提交到 outlet。 | RouteOutletCommitPlan。 | RouteOutletCommitResult。 | 失败不替换旧 content；old deactivate 拒绝则中止。 | attach 前可取消；attach 后完成回滚或提交。 | 同一 outlet commit 串行。 |
| VisualLifecycleHub.Publish | 发布 attach/detach/focus 等 visual 事件。 | VisualLifecycleEvent。 | void 或 result。 | handler 失败被隔离并诊断。 | 按 dispatcher 策略执行。 | 事件顺序按 UI 捕获顺序。 |
| PresentationPluginUnloadCoordinator.CoordinateAsync | 插件卸载前撤销 UI contribution。 | plugin id 和 unload request。 | PresentationPluginUnloadResult。 | active view 拒绝关闭或资源撤销失败时阻止 unload。 | 必须观察 token。 | 同一 plugin unload 串行且幂等。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ActivePluginView` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivePluginViewRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ActivePluginViewRegistryServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AvaloniaUiDispatcher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AvaloniaUiDispatcherServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `BoundViewHandle` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandTextDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CultureFlowDirectionApplier` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CultureResourceDictionaryApplier` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CurrentThreadCultureApplier` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ErrorMessageDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IActivePluginViewLease` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IActivePluginViewRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICommandBindingHandle` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IInteractionHandlerRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedCommandTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedErrorMessageTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedInteractionTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedNotificationTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedRouteTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedValidationMessageTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ILocalizedWindowTextTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationCultureApplier` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationFlowDirectionTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationPluginUnloadCoordinator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationResourceDictionaryRevoker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationResourceDictionaryTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationResourceLease` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationResourceRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPresentationRuntime` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouteOutlet` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IUiCommandSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IValidationVisualStateTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IViewDataContextAware` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IViewLocator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IViewRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionHandlerRegistrationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionHandlerRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InteractionTextDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedCommandTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedErrorMessageBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedInteractionTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedNotificationTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedRouteTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedValidationMessageBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LocalizedWindowTextBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NotificationTextDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationDiagnosticIds` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationFlowDirection` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationInteractionServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationLocalizationBridge` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationLocalizationServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadCoordinator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadCoordinatorServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadErrorKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationPluginUnloadResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationResourceContribution` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationResourceDictionaryRevocation` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationResourceDictionaryRevoker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationResourceRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationResourceRegistryServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationRuntime` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationRuntimeServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PresentationRuntimeState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteOutlet` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteOutletCommitPlan` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteOutletCommitResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteOutletOperation` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UiCommandState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UiStateFeedbackKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UiStateFeedbackPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationVisualStateBinding` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ValidationVisualStateSnapshot` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

| `ViewBinder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewFactory` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewFactoryContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewForAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewLookupRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewRegistryServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewRegistrationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `VisualLifecycleEvent` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `VisualLifecycleEventKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `VisualLifecycleHub` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `WindowTextDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
