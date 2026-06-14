# AtomUI.City.State Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCSTA001` | ChangedEventHandlerFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA002` | SubscriptionHandlerFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA003` | ApplicationStateNotRegistered | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA004` | ApplicationStateWriteDenied | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA005` | ComputedStateComputeFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA006` | WritableStateUpdateFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA007` | SnapshotRestoreFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA008` | ApplicationStateAlreadyRegistered | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA009` | StateScopeDisposeFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |
| `AUCSTA010` | ComputedStateDisposeFailed | `src/AtomUI.City.State/StateDiagnosticIds.cs` |

## 产品级必须诊断的失败

- 未注册 key：抛 StateNotRegisteredException 并诊断。
- 写入只读 state：抛 StateAccessDeniedException。
- 订阅回调失败：记录 subscriptionId，不回滚已提交状态。
- `AUCSTA001` 到 `AUCSTA010` 必须保持唯一、格式稳定，并覆盖 changed event、subscription、registry、access、computed、update、snapshot restore、scope dispose 和 computed dispose 失败。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 稳定 Context Key

| Code | Required Context |
| --- | --- |
| `AUCSTA001` | `valueType`、`version`；collection changed event 还应包含 `keyType`、`itemType`、`changeCount`。 |
| `AUCSTA002` | `dispatchPolicy`、`version`。 |
| `AUCSTA003` | `stateKey`、`valueType`。 |
| `AUCSTA004` | `accessPolicy`、`stateKey`、`valueType`。 |
| `AUCSTA005` | `valueType`。 |
| `AUCSTA006` | `stateKey`、`valueType`、`version`。 |
| `AUCSTA007` | `reason`、`stateKey`、`valueType`。 |
| `AUCSTA008` | `stateKey`、`valueType`。 |
| `AUCSTA009` | `scopeId`。 |
| `AUCSTA010` | `valueType`。 |

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.State.Tests` 必须断言当前源码诊断码、唯一性、格式、severity 和稳定 context key。
