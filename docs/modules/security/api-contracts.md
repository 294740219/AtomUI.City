# AtomUI.City.Security API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Authentication | AuthenticationStateStore, AuthenticationStateSnapshot | 当前用户状态。 | snapshot 不可变，跨线程读取一致。 |
| Permission | PermissionRegistry, IPermissionChecker | 权限定义和检查。 | 未注册权限稳定失败。 |
| Authorization | AuthorizationEvaluator, AuthorizationPolicy | 策略评估。 | 不操作 UI 或导航。 |
| Route/Command Integration | SecurityRouteGuard, CommandAuthorizationSource | 把授权结果暴露给 Routing 和 Presentation/MVVM。 | 只返回 result 或 state，不执行 UI。 |
| Token | IAccessTokenProvider, AccessTokenResult | 为 Data 等模块提供 token。 | 失败返回 result，不抛随机异常。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| AuthenticationStateStore.SetState | 发布认证状态。 | snapshot 不得为 null。 | void 或 change result。 | 非法 transition 拒绝。 | 同步提交无 token。 | 订阅通知基于 immutable snapshot。 |
| IPermissionRegistry.Register | 注册权限。 | PermissionDescriptor 必须有 stable name 和 owner。 | registration handle 或 result。 | 重复 name、owner revoked 返回失败。 | 同步 API 无 token。 | 读并发安全，写串行。 |
| IPermissionChecker.IsGrantedAsync | 检查权限。 | principal、permission name、resource 可选。 | AuthorizationResult 或 bool result。 | 未注册、provider 异常稳定映射。 | 必须观察 token。 | 无共享 mutable result。 |
| IAuthorizationEvaluator.AuthorizeAsync | 评估 policy。 | AuthorizationRequest 包含 principal、policy、resource。 | AuthorizationResult。 | policy 缺失、requirement failed、异常映射为 Failed/Forbidden。 | 必须观察 token。 | 同一 request 不修改全局状态。 |
| SecurityRouteGuard.CanEnterAsync | 将 route policy 转成 guard result。 | RouteGuardContext。 | RouteGuardResult。 | 未登录返回 redirect hint 或 deny。 | 必须观察 token。 | 不执行导航。 |
| IAccessTokenProvider.GetTokenAsync | 获取 access token。 | AccessTokenRequest。 | AccessTokenResult。 | 不可用、刷新失败、取消有独立 status。 | 必须观察 token。 | 并发 refresh 合并或拒绝，行为必须测试。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AccessTokenRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AccessTokenResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AccessTokenResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateSnapshot` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthenticationStateStore` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationEvaluator` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequest` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequirement` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationRequirementKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AuthorizationResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationChangeReason` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandAuthorizationState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CommandUnauthorizedBehavior` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DelegateAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAuthenticationStateProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAuthorizationEvaluator` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICommandAuthorizationDescriptorProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICommandAuthorizationSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ICurrentPrincipalAccessor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPermissionChecker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IPermissionRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouteAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryCommandAuthorizationDescriptorProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryRouteAuthorizationPolicyProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionChecker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionRegistry` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PermissionRegistryChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityFailureKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityRouteGuard` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityRouteGuardResultCodes` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SecurityServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UnavailableAccessTokenProvider` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
