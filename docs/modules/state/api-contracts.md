# AtomUI.City.State API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Application State | IApplicationState, IApplicationStateWriter | 全局共享状态。 | 读写分离。 |
| State Values | IWritableState<T>, IReadOnlyState<T> | 单值状态。 | 原子提交后通知。 |
| Computed | IComputedState<T> | 派生状态。 | 依赖变更触发重算。 |
| Snapshot | StateSnapshot | 状态快照。 | 不可变。 |
| Collection State | IStateCollection<TKey, TItem>, StateCollection<TKey, TItem> | keyed collection state。 | 变更记录、item version、collection snapshot 和 dispose 生命周期稳定。 |
| Diagnostics | StateDiagnosticIds | 状态错误诊断。 | AUCSTA001-010 稳定，诊断记录必须包含可定位 context。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IWritableState<T>.Set / SetValue / Update | 写入或转换状态。 | value/updater。 | `Set` 无返回；`SetValue`/`Update` 返回是否提交变更。 | updater 为 null 抛 `ArgumentNullException`；`WritableState<T>` Dispose 后 `Set`、`SetValue`、`Update` 抛 `ObjectDisposedException`；ReadOnly access 抛 `StateAccessDeniedException` 并写 diagnostics；updater 失败保留旧值并写 diagnostics；callback 失败写 diagnostics。 | 同步提交，不隐式切线程。 | 并发写串行化；相等值不递增 version、不通知；access 拒绝发生在 updater 执行前。 |
| IReadOnlyState<T>.OnChange | 订阅状态。 | handler/options。 | IStateSubscription。 | handler/options 为 null 抛 `ArgumentNullException`；`WritableState<T>` Dispose 后抛 `ObjectDisposedException`；handler 或 dispatcher 失败写 diagnostics 且不回滚已提交状态。 | 调度由 StateDispatchPolicy；Background 不得阻塞状态提交；Dispatcher pending callback 执行前必须重新检查 Dispose 状态。 | subscription dispose 后不再通知；重复 dispose 幂等。 |
| WritableState<T>.Dispose | 结束可写状态生命周期。 | 无。 | 无。 | 重复 Dispose 不抛异常；Dispose 后读属性仍可读取，mutation、subscription 和 restore-style mutation 抛 `ObjectDisposedException`。 | 无。 | 清空现有 subscriptions；不在状态锁内调用 handler。 |
| IApplicationState.Get / OnChange; IApplicationStateWriter.GetWritable / Set / Update | 读取、订阅或写入注册状态。 | key 必须有效；OnChange handler 和 Update updater 不得为 null。 | IReadOnlyState<T>、IStateSubscription、IWritableState<T> 或提交结果。 | default key 抛 `ArgumentException` 且不写 not registered diagnostics；未注册 key 抛 `StateNotRegisteredException` 并写 diagnostics；handler/updater 为 null 抛 `ArgumentNullException`，且 Update 的 null updater 先于 registry lookup 被拒绝。 | 同步提交，不隐式切线程。 | 未注册状态不得隐式创建；写入规则由对应 `WritableState<T>` 合同决定；Snapshot restore 只允许 Persisted state。 |
| IStateCollection<TKey,TItem>.AddOrUpdate / AddOrUpdateRange / Remove / Clear / RestoreSnapshot / OnChange | 修改、恢复或订阅 keyed collection state。 | key、items、snapshot、handler/options 必须有效。 | 是否提交变更或 IStateSubscription。 | key/items/snapshot/handler/options 为 null 按标准参数异常；`StateCollection<TKey,TItem>` Dispose 后 mutation、restore 和 subscription API 抛 `ObjectDisposedException`。 | 同步提交，不隐式切线程。 | 并发写串行化；无变化不递增 version、不通知；subscription dispose 后不再通知。 |
| IStateCollection<TKey,TItem>.Dispose | 结束集合状态生命周期。 | 无。 | 无。 | 重复 Dispose 不抛异常；Dispose 后读属性、item version 查询和 snapshot 仍可读取，mutation、restore 和 subscription API 抛 `ObjectDisposedException`。 | 无。 | 清空现有 subscriptions；不在状态锁内调用 handler。 |
| ComputedState<T> constructor | 创建计算状态。 | compute 不能为 null；dependencies 不得为 null 且不得包含 null 项。 | ComputedState<T>。 | compute/dependencies 为 null 抛 `ArgumentNullException`；dependency 项为 null 抛 `ArgumentException`。 | 无。 | 先校验全部 dependency，再建立订阅；依赖订阅随 computed dispose 释放，订阅阶段部分失败会释放已建立订阅。 |
| ComputedState<T>.Value | 读取计算值。 | 无。 | 当前计算值。 | compute 失败保留上一有效值并写 diagnostics。 | 同步计算，不执行 IO。 | 依赖变化后无订阅者只标记 dirty，读取时才重算；依赖变化且有订阅者时立即重算并通知。 |
| StateDefinition.Create | 创建状态定义。 | key、lifetime、access、snapshotPolicy、schemaVersion 必须有效。 | StateDefinition<T>。 | default key 抛 `ArgumentException`；未知 enum 或 schemaVersion 小于 1 抛 `ArgumentOutOfRangeException`。 | 无。 | 创建结果不可变。 |
| StateSnapshotEntry constructor / init | 创建快照条目并保护 init 边界。 | stateName/valueType/version/schemaVersion 必须有效。 | StateSnapshotEntry。 | stateName 为 null 或空白抛 `ArgumentException`；valueType 为 null 抛 `ArgumentNullException`；version 小于 0 或 schemaVersion 小于 1 抛 `ArgumentOutOfRangeException`。 | 无。 | record init 属性不得绕过快照条目边界。 |
| StateSnapshot constructor | 创建快照。 | entries 不得为 null，且不得包含 null 项。 | StateSnapshot。 | entries 为 null 抛 `ArgumentNullException`；包含 null 项抛 `ArgumentException`。 | 无。 | entries 创建后不可变。 |
| StateCollectionSnapshot<TKey,TItem> constructor | 创建集合快照。 | collectionVersion 必须大于等于 0；items 不得为 null，且不得包含 null 项。 | StateCollectionSnapshot<TKey,TItem>。 | collectionVersion 小于 0 抛 `ArgumentOutOfRangeException`；items 为 null 抛 `ArgumentNullException`；包含 null 项抛 `ArgumentException`。 | 无。 | items 创建后不可变。 |
| StateCollectionSnapshotEntry<TKey,TItem> constructor / init | 创建集合快照条目并保护 init 边界。 | key 不得为 null；itemVersion 必须大于等于 0。 | StateCollectionSnapshotEntry<TKey,TItem>。 | key 为 null 抛 `ArgumentNullException`；itemVersion 小于 0 抛 `ArgumentOutOfRangeException`。 | 无。 | record init 属性不得绕过集合快照条目边界。 |
| StateCollectionChange<TKey,TItem> constructor / init | 创建集合变更记录并保护 init 边界。 | kind 必须为已定义枚举；key 不得为 null；collectionVersion/itemVersion 必须大于等于 0。 | StateCollectionChange<TKey,TItem>。 | 未知 kind、负 collectionVersion 或负 itemVersion 抛 `ArgumentOutOfRangeException`；key 为 null 抛 `ArgumentNullException`。 | 无。 | record init 属性不得绕过集合变更边界。 |
| StateCollectionChangedEventArgs<TKey,TItem> constructor | 创建集合变更事件参数。 | change 不得为 null；changes 不得为 null、空列表或包含 null 项。 | StateCollectionChangedEventArgs<TKey,TItem>。 | single change 为 null 抛 `ArgumentNullException`；changes 为 null 抛 `ArgumentNullException`；空列表或包含 null 项抛 `ArgumentException`。 | 无。 | `Version` 取最后一条 change 的 collection version；changes 创建后不可变。 |
| WritableState<T> constructor | 创建单值状态。 | initialValue；comparer、diagnostics、stateName 和 access 可选。 | WritableState<T>。 | 未知 access 抛 `ArgumentOutOfRangeException`；空 stateName 使用 value type name 作为诊断名称。 | 无。 | access policy 固定在实例生命周期内；ApplicationStateRegistry 必须把 StateDefinition access 传入 WritableState。 |

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

- `WritableState<T>` 的 `Value`、`Version`、`ValueType` 在 Dispose 后仍可读取。
- `WritableState<T>` 的 `Set`、`SetValue`、`Update`、`OnChange` 和 restore-style mutation 在 Dispose 后必须失败并抛 `ObjectDisposedException`。
- `StateCollection<TKey,TItem>` 的 `Version`、`Items`、`TryGetItemVersion` 和 `CreateSnapshot` 在 Dispose 后仍可读取。
- `StateCollection<TKey,TItem>` 的 `AddOrUpdate`、`AddOrUpdateRange`、`Remove`、`Clear`、`RestoreSnapshot` 和 `OnChange` 在 Dispose 后必须失败并抛 `ObjectDisposedException`。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
