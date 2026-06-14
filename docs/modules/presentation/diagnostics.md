# AtomUI.City.Presentation Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Code | 名称 | Severity | 场景 | Required Context |
| --- | --- | --- | --- | --- |
| `AUCPRS003` | DispatcherOperationRejected | Warning | runtime 未 ready、runtime stopping 或 Avalonia dispatcher unavailable。 | `operationId`, `targetAction`, `callingThreadId`, `dispatcherThreadId`, `error` |
| `AUCPRS004` | DispatcherCallbackFailed | Error | UI dispatcher work item 抛异常。 | `operationId`, `targetAction`, `callingThreadId`, `dispatcherThreadId`, `error` |
| `AUCPRS005` | ViewLocatorMatched | Info | ViewModel 精确 key lookup 命中 ViewDescriptor。 | `viewModelType`, `viewType`, `viewKey`, `routeId`, `ownerId`, `pluginId`, `contributionId` |
| `AUCPRS006` | ViewLocatorFailed | Warning | ViewModel 精确 key lookup 未命中。 | `viewModelType`, `viewKey`, `routeId`, `ownerId` |
| `AUCPRS007` | ViewCreated | Info | ViewFactory 创建 View 成功。 | `viewModelType`, `viewType`, `viewKey`, `constructorParameters`, `elapsedMilliseconds` |
| `AUCPRS008` | ViewCreationFailed | Error | ViewFactory 创建 View 失败。 | `viewModelType`, `viewType`, `viewKey`, `constructorParameters`, `elapsedMilliseconds`, `error` |
| `AUCPRS009` | ViewBound | Info | ViewBinder 设置 DataContext 并建立 BoundViewHandle。 | `viewModelType`, `viewType`, `viewKey`, `elapsedMilliseconds` |
| `AUCPRS010` | ViewBindingFailed | Error | ViewBinder binding 失败并释放已创建 View。 | `viewModelType`, `viewType`, `viewKey`, `elapsedMilliseconds`, `error` |
| `AUCPRS011` | OutletCommitPlanned | Info | RouteOutlet 收到 commit plan。 | `outletName`, `requestedOutletName`, `operation`, `currentViewType`, `newViewType` |
| `AUCPRS012` | OutletCommitSucceeded | Info | RouteOutlet 成功提交 replace 或 clear。 | `outletName`, `requestedOutletName`, `operation`, `currentViewType`, `newViewType` |
| `AUCPRS013` | OutletCommitFailed | Error | RouteOutlet commit 失败、outlet mismatch、dispatcher 失败或 rejected handle dispose 失败。 | `outletName`, `requestedOutletName`, `operation`, `currentViewType`, `newViewType`, `error` |
| `AUCPRS014` | VisualLifecycleAdapterExecuted | Info | Visual lifecycle handler 成功处理事件。 | `viewType`, `viewModelType`, `eventKind`, `error` |
| `AUCPRS015` | VisualLifecycleAdapterFailed | Error | Visual lifecycle handler 处理事件失败。 | `viewType`, `viewModelType`, `eventKind`, `error` |
| `AUCPRS016` | ResourceDictionaryRevoked | Info | Resource dictionary target 成功撤销 plugin 或 contribution 资源。 | `pluginId`, `contributionId`, `targetCount`, `errorKind`, `error` |
| `AUCPRS017` | ResourceDictionaryRevokeFailed | Error | Resource dictionary target 撤销失败。 | `pluginId`, `contributionId`, `targetCount`, `errorKind`, `error` |
| `AUCPRS018` | ResourceDictionaryApplied | Info | Resource dictionary target 成功应用 culture 和 packages。 | `culture`, `uiCulture`, `packageIds`, `targetCount`, `errorKind`, `error` |
| `AUCPRS019` | ResourceDictionaryApplyFailed | Error | Resource dictionary target 应用 culture 或 packages 失败。 | `culture`, `uiCulture`, `packageIds`, `targetCount`, `errorKind`, `error` |
| `AUCPRS020` | InteractionHandled | Info | Interaction handler 成功处理 request。 | `requestType`, `resultType`, `status`, `pluginId`, `contributionId`, `error` |
| `AUCPRS021` | InteractionNotHandled | Warning | Interaction request 没有可用 handler。 | `requestType`, `resultType`, `status`, `pluginId`, `contributionId`, `error` |
| `AUCPRS022` | InteractionFailed | Error | Interaction handler 抛异常。 | `requestType`, `resultType`, `status`, `pluginId`, `contributionId`, `error` |
| `AUCPRS023` | InteractionHandlerRevoked | Info | Interaction handler 被 plugin 或 contribution revoke。 | `requestType`, `resultType`, `status`, `pluginId`, `contributionId`, `error` |
| `AUCPRS024` | ValidationVisualStateApplied | Info | ValidationScope snapshot 应用到 visual target。 | `status`, `keys`, `messageCount`, `targetType`, `error` |
| `AUCPRS025` | ValidationVisualStateApplyFailed | Error | Validation visual target 应用失败。 | `status`, `keys`, `messageCount`, `targetType`, `error` |
| `AUCPRS028` | ResourceContributionRegistered | Info | Presentation resource contribution 注册。 | `kind`, `pluginId`, `contributionId`, `resourceType`, `error` |
| `AUCPRS029` | ResourceContributionRevoked | Info | Presentation resource contribution 撤销。 | `kind`, `pluginId`, `contributionId`, `resourceType`, `error` |
| `AUCPRS030` | ResourceContributionRevokeFailed | Error | Presentation resource contribution 撤销失败。 | `kind`, `pluginId`, `contributionId`, `resourceType`, `error` |

## 产品级必须诊断的失败

- View 未注册：返回失败并诊断。
- 非 UI 线程提交：marshal 到 dispatcher；dispatcher 不可用或 work 失败必须诊断。
- View lookup 未注册或 owner 已撤销：返回失败并诊断。
- View 创建失败：不替换现有 outlet。
- View binding 失败：释放已创建 View 并诊断。
- Outlet commit 失败：保留旧 content，释放被拒绝的新 handle，并记录 outlet、operation、view type 和 error。
- Visual lifecycle handler 失败：记录失败并继续通知后续 handler。
- Interaction handler 缺失、失败或撤销：记录 request/result type、owner 和 status。
- Validation visual target 失败：记录 status、keys、message count、target type 和 error；用户取消不记录失败。
- Culture 或 resource dictionary 局部失败：记录失败并继续刷新或撤销后续 target，返回首个失败。
- Presentation resource contribution 撤销失败：记录失败并继续撤销同 plugin 或 contribution 下的其他资源。
- 插件卸载 active view：detach 并撤销资源。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`ownerId`、`viewModelType`、`viewType`、`viewKey`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`callingThreadId`、`dispatcherThreadId`、`targetAction`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Presentation.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
