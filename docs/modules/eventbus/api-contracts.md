# AtomUI.City.EventBus API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Publish | IEventBus, IEventPublisher, EventPublishOptions, EventPublishResult | 发布类型化事件。 | 返回每个 handler 结果。 |
| Subscribe | IEventSubscriber, EventSubscriberExtensions, IEventHandler<TEvent>, IEventSubscription | 注册和释放 handler。 | 每条动态订阅必须绑定且只能绑定一个 owner；owner 释放只撤销其拥有的订阅。 |
| Contract | IEventContractRegistry, EventContractDescriptor | 事件边界声明。 | 跨插件 contract 必须来自共享程序集。 |
| Diagnostics | EventDiagnosticIds | 现有 EventBus.* 诊断。 | 保持现有字符串兼容；delivery failure/cancellation 诊断必须包含 contractId、eventId 和 subscriptionId context。 |

## 关键方法合同

### Subscription public surface

`AUC-EVENTBUS-002` 的目标 public surface 固定为：

```csharp
public interface IEventHandler<TEvent>
{
    ValueTask HandleAsync(EventContext<TEvent> context);
}

public interface IEventSubscriber
{
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);
}

public static class EventSubscriberExtensions
{
    public static IEventSubscription Subscribe<TEvent>(
        this IEventSubscriber subscriber,
        LifecycleScope owner,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null);
}

public interface IEventSubscription : IDisposable, IAsyncDisposable
{
    EventSubscriptionId Id { get; }
    EventSubscriptionState State { get; }
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

- `LifecycleScope` 是 Application Plane 动态订阅的 public owner 类型；不引入当前 Core 中不存在的 `ILifecycleScope`。
- `IEventHandler<TEvent>` 保持 invariant。`EventContext<TEvent>` 不能支持安全的 `in TEvent` 合同。
- 核心接口不提供 ownerless overload，也不提供 `Func<TEvent, CancellationToken, ValueTask>` 简化入口。
- 同步 `Action<EventContext<TEvent>>` 只是便利扩展，必须显式传入 owner，并转接到异步核心入口。
- 静态/DI handler 的 owner 由 generated registration 和 EventBus Host controller 绑定为 `ApplicationScope`，不通过 ownerless public API 绕过所有权。
- 插件 handler 由 EventBus 领域 ContributionLease 持有，不允许插件把私有生命周期冒充 Host `LifecycleScope`。

`EventSubscriptionState` 的 1.0 值固定为：

| Value | Name | Meaning |
| ---: | --- | --- |
| 0 | `Created` | runtime 已分配，尚未提交到 active snapshot。 |
| 1 | `Active` | 可以获取新的 delivery。 |
| 2 | `Quiescing` | 已从新 snapshot 移除并触发取消。 |
| 3 | `Draining` | 等待已获取的 delivery 和 handler 清理。 |
| 4 | `Disposed` | handler、队列、注册和引用已经释放。 |
| 5 | `Faulted` | 已停止接收 delivery，但终止或清理失败。 |

Timeout 或调用方取消只结束当前等待，不形成永久 `StopTimedOut` 状态；唯一终止事务继续执行并最终进入 `Disposed` 或 `Faulted`。

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| IEventPublisher.PublishAsync | 发布事件。 | event 实例非 null，options 可选。 | EventPublishResult。 | event 为 null 抛 `ArgumentNullException`；disposed bus 抛 `ObjectDisposedException`；contract 非法、handler 失败、取消。 | 进入 contract registry、diagnostics 和 subscription snapshot 前必须观察 token；无订阅者时也不能把已取消 publish 报告为成功。 | 并发 publish 使用 subscription snapshot。 |
| IEventPublisher.PostAsync | 接受异步发布请求。 | event 实例非 null，options 可选。 | EventPostResult。 | event 为 null 抛 `ArgumentNullException`；disposed bus 抛 `ObjectDisposedException`；已取消 token 返回 rejected result。 | 接受前必须观察 token，接受后 delivery 取消进入 diagnostics。 | 返回的 EventId 必须用于后续 delivery。 |
| `IEventSubscriber.Subscribe<TEvent>` | 创建一条绑定单一 owner 的动态订阅。 | owner、handler 非 null；owner 必须为 `Running`；options 可为 null 并采用声明的默认值。 | 独立的 `IEventSubscription`。 | owner/handler 为 null 抛 `ArgumentNullException`；owner 非 Running、EventBus 不再接受订阅或 contract 非法时在提交前失败。 | 注册操作不接受 token；提交后由 owner/subscription token 管理生命周期。 | EventBus accepting 状态、owner 状态、owner token registration、状态转换和 snapshot 提交组成一个原子提交；与 owner/EventBus stop 竞争时不得留下 Active 或半注册订阅。 |
| `IEventHandler<TEvent>.HandleAsync` | 处理强类型 delivery。 | context 非 null，并携带当前 delivery 的组合 token。 | `ValueTask`，完成表示本次 handler delivery 完成。 | 同步或异步异常进入订阅错误策略。 | context token 组合 publisher、owner/subscription 和 EventBus shutdown；handler 必须向下游异步操作传播。 | EventBus 不在内部锁内调用；并发度服从 subscription/channel policy。 |
| `EventSubscriberExtensions.Subscribe<TEvent>` | 为同步 handler 提供有 owner 的便利入口。 | subscriber、owner、handler 非 null；options 可为 null。 | 核心异步 Subscribe 返回的 `IEventSubscription`。 | 非法参数使用标准参数异常；其余失败与核心 Subscribe 一致。 | 与核心 Subscribe 一致。 | 只适配 delegate，不建立第二套注册或生命周期逻辑。 |
| IEventBus.Dispose | 结束进程内事件总线生命周期。 | 无。 | 无。 | 重复 Dispose 不抛异常；Dispose 后 publish、post 和 subscribe API 抛 `ObjectDisposedException`。 | 无。 | 清空并释放 active subscriptions；DI provider dispose 必须释放 singleton bus。 |
| EventBusServiceCollectionExtensions.AddEventBus | 注册 EventBus 默认服务。 | services 非 null。 | 原 IServiceCollection。 | services 为 null 抛 `ArgumentNullException`。 | 无。 | 使用 TryAdd 语义注册默认 IHostDiagnostics、IEventContractRegistry、IEventBus、IEventPublisher 和 IEventSubscriber；调用方预注册 diagnostics 不被覆盖。 |
| IEventContractRegistry.Register | 注册 shared event contract descriptor。 | descriptor 非 null 且 plane 必须为 Shared。 | 无。 | descriptor 为 null 抛 `ArgumentNullException`；plugin-private descriptor、重复 id 或重复 type 抛 `InvalidOperationException`。 | 无。 | 注册表内 contract id 和 event type 映射保持稳定；重复注册不得静默覆盖。 |
| `IEventSubscription.Dispose` | 快速撤销订阅。 | 可重复调用。 | 无。 | 不同步传播 in-flight handler 的清理失败；失败由共享终止事务和 diagnostics 观察。 | 触发 subscription cancellation，但不等待 handler。 | 原子执行 Active -> Quiescing、移出新 snapshot 并发布唯一终止事务；不得同步等待异步 handler。 |
| `IEventSubscription.DisposeAsync / StopAsync` | 确定性停止并释放订阅。 | 可重复调用；StopAsync token 只控制当前等待。 | 共享终止事务完成时状态为 Disposed，失败时为 Faulted。 | 一个清理失败传播原异常；多个失败使用 `AggregateException`，同时写 diagnostics 并继续清理其余资源。 | owner/Dispose 触发终止后不能由调用方 token 撤销；已 Disposed 后再次 StopAsync 即使 token 已取消也作为 no-op。 | 并发 Dispose、DisposeAsync、StopAsync 和 owner cancellation 共享唯一终止事务；Quiescing 后不得开始新 handler。 |
| EventPublishResult constructor | 创建发布结果。 | eventId 必须非空，contractId 必须已创建，deliveries 不得为 null 且不得包含 null 项。 | EventPublishResult。 | eventId 为空、contractId 为 default 或 deliveries 包含 null 项抛 `ArgumentException`；deliveries 为 null 抛 `ArgumentNullException`。 | 无。 | delivery 列表创建后不可由外部 mutation 改变。 |
| EventDeliveryResult constructor/init | 创建单个订阅 delivery 结果。 | subscriptionId 必须已创建，dispatchPolicy 必须是已定义值，成功结果不得同时取消或携带 error message。 | EventDeliveryResult。 | constructor 或 init mutation 中 subscriptionId 为 default、成功且取消、成功且 error message 非 null 抛 `ArgumentException`；未知 dispatchPolicy 抛 `ArgumentOutOfRangeException`。 | 无。 | constructor 与 init mutation 均保持同一状态一致性。 |
| EventPostResult constructor/init | 创建 post 接受或拒绝结果。 | eventId 必须非空，contractId 必须已创建；Accepted 为 true 时 rejection reason 必须为 null，Accepted 为 false 时必须有非空白 rejection reason。 | EventPostResult。 | constructor 或 init mutation 中 eventId 为空、contractId 为 default 或 accepted/rejection reason 状态不一致时抛 `ArgumentException`。 | 无。 | constructor 与 init mutation 均保持同一状态一致性。 |
| EventPublishOptions | 描述 publish 上下文。 | `PublishDepth` 必须大于等于 0；`CorrelationId`/`CausationId` 为 null 或稳定 id，不得为空白、包含首尾空白或控制字符。 | EventPublishOptions。 | publish depth 小于 0 时，options init 和发布入口抛 `ArgumentOutOfRangeException`；CorrelationId/CausationId 非法时抛 `ArgumentException`。 | 无。 | 调用内只读取，不回写 options。 |
| EventContext<TEvent> constructor | 创建 handler 执行上下文。 | eventData 非 null，contractId/subscriptionId 必须已创建，eventId 必须非空，correlationId 必须为稳定 id，causationId 为 null 或稳定 id，二者不得为空白、包含首尾空白或控制字符，publishDepth 必须大于等于 0，dispatchPolicy 必须是已定义值。 | EventContext<TEvent>。 | eventData 为 null 抛 `ArgumentNullException`；contractId/subscriptionId 为 default、eventId 为空或 correlationId/causationId 无效抛 `ArgumentException`；publishDepth 小于 0 或未知 dispatchPolicy 抛 `ArgumentOutOfRangeException`。 | 仅保存 token，不主动取消。 | 创建后不可变。 |
| EventSubscriptionOptions.WithErrorPolicy | 派生错误策略选项。 | errorPolicy 必须是已定义 enum 值。 | EventSubscriptionOptions。 | 未知 error policy 抛 `ArgumentOutOfRangeException`；FailPublisher handler 失败传播异常；StopPublication 不再开始剩余 delivery；ContinueAndReport 聚合失败并继续。 | 无。 | 不修改原 options，返回新实例。 |
| EventContractDescriptor.Shared / PluginPrivate | 创建事件 contract descriptor。 | contractId 必须已创建；Shared assembly 必须与事件类型定义程序集一致。 | EventContractDescriptor。 | contractId 为 default 抛 `ArgumentException`；Shared assembly 为 null 抛 `ArgumentNullException`；Shared assembly 不匹配抛 `InvalidOperationException`。 | 无。 | descriptor 创建后不可变。 |
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
| `EventSubscriberExtensions` | 支持类型 | 只提供有 owner 的同步 delegate 适配，不建立独立注册或生命周期路径。 |
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

- `IEventBus.Dispose` 幂等，并释放 active subscriptions。
- `IEventBus.PublishAsync`、`IEventBus.PostAsync` 和 `IEventSubscriber.Subscribe` 在 Dispose 后必须失败并抛 `ObjectDisposedException`。
- `IEventSubscription.Dispose` 幂等、快速建立 Quiescing barrier 并触发唯一终止事务，但不等待 in-flight handler。
- `IEventSubscription.StopAsync` 与 `DisposeAsync` 等待同一终止事务；调用方取消等待后，终止和清理继续进行。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
