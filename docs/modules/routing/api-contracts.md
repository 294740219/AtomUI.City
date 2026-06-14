# AtomUI.City.Routing API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Route Definition | RouteTemplate, RouteDefinitionAttribute, RouteMapAttribute | 声明和解析路由模板。 | 模板解析结果不可变，非法模板必须稳定失败。 |
| Route Graph | RouteDescriptor, RouteGraphSnapshot, RouteGraphError | 合成不可变路由图。 | 发布是原子操作，失败不替换旧 graph。 |
| Navigation | IRouter, NavigationScope, NavigationResult | 执行导航事务。 | 失败、取消或 guard 拒绝不能提交半导航。 |
| Target Resolution | NavigationTarget, ViewModelTargetDescriptor | 输出 ViewModel target。 | 不创建 View、不操作 VisualTree。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| RouteTemplate.Parse | 解析 route pattern。 | pattern 不得为空；constraint 必须可识别。 | RouteTemplate 或声明异常。 | 非法 segment、重复参数名、catch-all 位置错误。 | 纯 CPU，无 token。 | 无共享状态，可并发。 |
| RouteTemplate.TryMatch | 对单个模板匹配 path。 | path 不得为 null。 | bool 和只读参数字典。 | constraint 拒绝或 segment 不匹配返回 false。 | 纯 CPU，无 token。 | 无共享状态，可并发。 |
| RouteGraphSnapshot.Create | 从 descriptors 发布 graph。 | descriptors 稳定排序且 owner 明确。 | RouteGraphSnapshot。 | 重复 id、同级模板冲突、缺失 parent 返回 RouteGraphError。 | 批量 build 应观察 token。 | 发布后不可变；旧 snapshot 继续可读。 |
| RouteGraphSnapshot.WithoutContribution | 按 contribution 发布撤销后的新 graph。 | contributionId 必须非空；version 可显式指定。 | 新 RouteGraphSnapshot。 | 撤销后剩余 route graph 非法时返回 RouteGraphError。 | 纯 CPU，无 token。 | 旧 snapshot 不变；新 snapshot 版本单调递增。 |
| RouteGraphSnapshot.WithContribution | 按 contribution 发布添加后的新 graph。 | contributionId 必须非空；route 必须声明同一 contributionId。 | 新 RouteGraphSnapshot。 | 冲突或 contribution mismatch 返回 RouteGraphException；旧 snapshot 不变。 | 纯 CPU，无 token。 | 旧 snapshot 不变；新 snapshot 版本单调递增。 |
| RouteMatcher.Match / MatchAll | 在 immutable snapshot 上匹配 path。 | path 不得为 null。 | RouteMatch 或只读 RouteMatch 列表。 | 无匹配返回 NotFound 或空列表；constraint 拒绝不进入 guard。 | 纯 CPU，无 token。 | 允许多线程并发读取。 |
| IRouter.NavigateAsync | 执行导航事务。 | target route、parameters、NavigationOptions。 | NavigationResult。 | match/guard/resolver/commit 任一失败都不改变 current snapshot；busy 返回 `CITY-NAVIGATION-BUSY`。 | 取消后返回 Cancelled，不能提交。 | `CancelPrevious` 取消旧事务，`Queue` 排队，`RejectIfBusy` 立即拒绝。 |
| IRouteEnterGuard.CanEnterAsync | 进入 route 前授权或重定向。 | RouteGuardContext 必须包含 route、parameters、services。 | RouteGuardResult。 | 异常映射为 navigation failed；redirect loop 返回 `CITY-NAVIGATION-REDIRECT-LOOP`。 | 必须观察 token。 | 按 route hierarchy root-to-leaf 执行，遇到非 Allow 立即停止。 |
| IRouteLeaveGuard.CanLeaveAsync | 离开当前 route 前确认。 | RouteGuardContext 使用当前 route 和当前 snapshot 参数。 | RouteGuardResult。 | Reject/Cancel/Failed 不改变 current snapshot。 | 必须观察 token。 | 按 route hierarchy leaf-to-root 执行，遇到非 Allow 立即停止。 |
| NavigationResult.RedirectTarget | 暴露 redirect 目标。 | 仅 Redirected 结果设置。 | NavigationTarget 或 null。 | 非 redirect 结果为 null。 | 纯数据。 | Result 不可变。 |
| NavigationScope.DisposeAsync | 结束导航作用域并释放临时资源。 | 允许重复调用。 | ValueTask。 | Dispose 后新导航返回 `CITY-NAVIGATION-SCOPE-DISPOSED`。 | 取消运行中导航。 | Dispose 幂等。 |
| ViewModelTargetDescriptor | 描述 route 的 ViewModel target。 | ViewModelType 必须为稳定类型；parameter bindings 会复制为只读。 | target descriptor。 | 缺失或不可构造 target 在导航提交前失败。 | 纯数据，无 token。 | 不创建 ViewModel；不依赖 Presentation。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `IRouteEnterGuard` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouteLeaveGuard` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouteMatchPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IRouter` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IndexRouteAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LayoutRouteAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationConcurrencyPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationHistoryBehavior` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationMode` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationScope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationSnapshot` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationTarget` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `NavigationTargetKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RedirectRouteAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteDefinitionKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteExtensionPoint` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteExtensionPointAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGraphError` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGraphException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGraphSnapshot` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGroupAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGuardContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGuardResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteGuardResultStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMapAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMatch` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMatchPolicyContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMatchStatus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMatcher` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteMetadataDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteReference` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteReference<TParameters>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteTemplate` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteTemplateSegment` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteTemplateSegmentKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ViewModelTargetDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Route Graph 贡献合同

- `RouteDescriptor.ContributionId` 为可选来源贡献标识；Host 路由可以为空，插件或动态贡献必须设置。
- `RouteGraphSnapshot.GetContributionRoutes` 返回只读 contribution route 列表，未知 contribution 返回空集合。
- `RouteGraphSnapshot.WithContribution` 只发布新 snapshot，不修改旧 snapshot；冲突只拒绝本次 contribution。
- 同级同 outlet 同 template 默认冲突；带 match policy 的候选允许共存，由导航阶段逐个 policy 过滤。

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
