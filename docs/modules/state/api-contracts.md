# AtomUI.City.State API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Application State | IApplicationState, IApplicationStateWriter | 全局共享状态。 | 读写分离。 |
| State Values | IWritableState<T>, IReadOnlyState<T> | 单值状态。 | 原子提交后通知。 |
| Computed | IComputedState<T> | 派生状态。 | 依赖变更触发重算。 |
| Snapshot | StateSnapshot | 状态快照。 | 不可变。 |
| Collection State | StateCollection<TKey, TItem> | keyed collection state。 | 变更记录、item version 和 collection snapshot 稳定。 |
| Diagnostics | StateDiagnosticIds | 状态错误诊断。 | AUCSTA001-010 稳定。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IWritableState<T>.Set | 写入状态。 | value。 | 新 version。 | 写入拒绝、callback 失败。 | 同步提交，不隐式切线程。 | 并发写串行化。 |
| IReadOnlyState<T>.Subscribe | 订阅状态。 | handler/options。 | IStateSubscription。 | disposed state 失败；handler 失败写 diagnostics。 | 调度由 StateDispatchPolicy；Background 不得阻塞状态提交。 | dispose 后不再通知。 |
| ComputedState<T> constructor | 创建计算状态。 | compute 不能为 null；dependencies 不得为 null 且不得包含 null 项。 | ComputedState<T>。 | compute/dependencies 为 null 抛 `ArgumentNullException`；dependency 项为 null 抛 `ArgumentException`。 | 无。 | 依赖订阅随 computed dispose 释放。 |
| ComputedState<T>.Value | 读取计算值。 | 无。 | 当前计算值。 | compute 失败保留上一有效值并写 diagnostics。 | 同步计算，不执行 IO。 | 依赖变化后无订阅者只标记 dirty，读取时才重算；依赖变化且有订阅者时立即重算并通知。 |
| StateDefinition.Create | 创建状态定义。 | key、lifetime、access、snapshotPolicy、schemaVersion 必须有效。 | StateDefinition<T>。 | 未知 enum 或 schemaVersion 小于 1 抛 `ArgumentOutOfRangeException`。 | 无。 | 创建结果不可变。 |
| StateSnapshotEntry constructor | 创建快照条目。 | stateName/valueType/version/schemaVersion 必须有效。 | StateSnapshotEntry。 | version 小于 0 或 schemaVersion 小于 1 抛 `ArgumentOutOfRangeException`。 | 无。 | record init 属性只用于不可变快照载体。 |
| StateSnapshot constructor | 创建快照。 | entries 不得为 null，且不得包含 null 项。 | StateSnapshot。 | entries 为 null 抛 `ArgumentNullException`；包含 null 项抛 `ArgumentException`。 | 无。 | entries 创建后不可变。 |
| StateCollectionSnapshot<TKey,TItem> constructor | 创建集合快照。 | collectionVersion 必须大于等于 0；items 不得为 null，且不得包含 null 项。 | StateCollectionSnapshot<TKey,TItem>。 | collectionVersion 小于 0 抛 `ArgumentOutOfRangeException`；items 为 null 抛 `ArgumentNullException`；包含 null 项抛 `ArgumentException`。 | 无。 | items 创建后不可变。 |
| StateCollectionSnapshotEntry<TKey,TItem> constructor | 创建集合快照条目。 | key 不得为 null；itemVersion 必须大于等于 0。 | StateCollectionSnapshotEntry<TKey,TItem>。 | key 为 null 抛 `ArgumentNullException`；itemVersion 小于 0 抛 `ArgumentOutOfRangeException`。 | 无。 | 条目创建后作为快照载体使用。 |
| StateCollectionChange<TKey,TItem> constructor | 创建集合变更记录。 | kind 必须为已定义枚举；key 不得为 null；collectionVersion/itemVersion 必须大于等于 0。 | StateCollectionChange<TKey,TItem>。 | 未知 kind、负 collectionVersion 或负 itemVersion 抛 `ArgumentOutOfRangeException`；key 为 null 抛 `ArgumentNullException`。 | 无。 | 变更记录不可回写到集合，只用于通知和诊断。 |
| StateCollectionChangedEventArgs<TKey,TItem> constructor | 创建集合变更事件参数。 | change 不得为 null；changes 不得为 null、空列表或包含 null 项。 | StateCollectionChangedEventArgs<TKey,TItem>。 | changes 为 null 抛 `ArgumentNullException`；空列表或包含 null 项抛 `ArgumentException`。 | 无。 | `Version` 取最后一条 change 的 collection version；changes 创建后不可变。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ApplicationStateRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ComputedState<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationStateWriter` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IComputedState<T>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IReadOnlyState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IReadOnlyState<T>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateCollection<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateReaction` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateSubscription` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IStateValue<out T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IWritableState<T>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateAccessDeniedException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateAccessPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateChangedEventArgs` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateChangedEventArgs<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollection<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollectionChange<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollectionChangeKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollectionChangedEventArgs<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollectionSnapshot<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateCollectionSnapshotEntry<TKey, TItem>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateDefinition` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateDefinition<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateDispatchPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateKey<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateLifetime` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateNotRegisteredException` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateScope` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateScopeState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateSnapshot` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateSnapshotEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateSnapshotPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `StateSubscriptionOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `WritableState<T>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
