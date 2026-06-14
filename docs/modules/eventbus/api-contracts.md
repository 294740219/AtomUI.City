# AtomUI.City.EventBus API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Publish | IEventPublisher, EventPublishOptions, EventPublishResult | 发布类型化事件。 | 返回每个 handler 结果。 |
| Subscribe | IEventSubscriber, IEventHandler<TEvent>, IEventSubscription | 注册和释放 handler。 | owner 释放必须撤销。 |
| Contract | IEventContractRegistry, EventContractDescriptor | 事件边界声明。 | 跨插件 contract 必须来自共享程序集。 |
| Diagnostics | EventDiagnosticIds | 现有 EventBus.* 诊断。 | 保持现有字符串兼容。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IEventPublisher.PublishAsync | 发布事件。 | event 实例非 null，options 可选。 | EventPublishResult。 | event 为 null 抛 `ArgumentNullException`；contract 非法、handler 失败、取消。 | 进入 contract registry、diagnostics 和 subscription snapshot 前必须观察 token；无订阅者时也不能把已取消 publish 报告为成功。 | 并发 publish 使用 subscription snapshot。 |
| IEventPublisher.PostAsync | 接受异步发布请求。 | event 实例非 null，options 可选。 | EventPostResult。 | event 为 null 抛 `ArgumentNullException`；已取消 token 返回 rejected result。 | 接受前必须观察 token，接受后 delivery 取消进入 diagnostics。 | 返回的 EventId 必须用于后续 delivery。 |
| IEventSubscriber.Subscribe | 订阅事件。 | handler 非 null，owner/options 可选。 | IEventSubscription。 | Disposed bus 或非法 contract 失败。 | 无异步取消。 | 不得在锁内调用 handler。 |
| IEventSubscription.DisposeAsync | 释放订阅。 | 可重复调用。 | Disposed 状态。 | 释放中 handler 失败进入 diagnostics。 | 取消只影响等待。 | 并发 dispose 幂等。 |
| EventSubscriptionOptions.WithErrorPolicy | 派生错误策略选项。 | errorPolicy 必须是已定义 enum 值。 | EventSubscriptionOptions。 | 未知 error policy 抛 `ArgumentOutOfRangeException`。 | 无。 | 不修改原 options，返回新实例。 |
| EventContractId constructor | 创建稳定事件 contract id。 | value 非 null、非空白、不得包含首尾空白或控制字符。 | EventContractId。 | value 为 null、空白、包含首尾空白或控制字符时抛 `ArgumentException`。 | 无。 | 创建结果不可变。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `EventBusServiceCollectionExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContext<TEvent>` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractDescriptor` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractId` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventContractPlane` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDispatchPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventErrorPolicy` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPostResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPublishOptions` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventPublishResult` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventDeliveryResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionId` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `EventSubscriptionState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventBus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventPublisher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventSubscriber` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventContractRegistry` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventHandler<TEvent>` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IEventSubscription` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryEventBus` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryEventContractRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
