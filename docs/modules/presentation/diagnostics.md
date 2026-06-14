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

## 产品级必须诊断的失败

- View 未注册：返回失败并诊断。
- 非 UI 线程提交：marshal 到 dispatcher；dispatcher 不可用或 work 失败必须诊断。
- View lookup 未注册或 owner 已撤销：返回失败并诊断。
- View 创建失败：不替换现有 outlet。
- View binding 失败：释放已创建 View 并诊断。
- Outlet commit 失败：保留旧 content，释放被拒绝的新 handle，并记录 outlet、operation、view type 和 error。
- 插件卸载 active view：detach 并撤销资源。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`ownerId`、`viewModelType`、`viewType`、`viewKey`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`callingThreadId`、`dispatcherThreadId`、`targetAction`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Presentation.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
