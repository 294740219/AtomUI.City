# AtomUI.City.EventBus Dispatching 合同

## 适用范围

本专题属于 `AtomUI.City.EventBus` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Dispatching` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。

## Public Contract

- 只允许通过 `AtomUI.City.EventBus` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publication | EventPublicationTests |
| AUC-EVENTBUS-004 | Dispatch & Failure Policy | EventDispatchAndFailurePolicyTests |
| AUC-EVENTBUS-007 | Bounded Channel Runtime | EventChannelRuntimeTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.EventBus Dispatching 设计

适用范围：事件发布完成语义、Core Threading 集成、channel 队列、顺序保证、并发、背压、取消、递归发布和性能设计。

### 1. 定位

Dispatching 决定事件从 publisher 到 handler 的执行路径。

线程安全集合只能保证内部数据结构不损坏，不能保证应用观察到的事件顺序、handler 重入、插件停用屏障和发布完成语义。EventBus 必须把这些行为明确为公共 contract。

Dispatching 复用 Core 的 `IUiDispatcher` 和 lifecycle cancellation，并由 EventBus 提供：

- `IEventBackgroundScheduler`
- `EventDispatchPolicy`
- `EventDispatchMode`
- Lifecycle cancellation
- Diagnostics/ErrorPolicy

### 2. PublishAsync 与 PostAsync

#### PublishAsync

`PublishAsync` 是等待处理完成的发布方式。

完成条件：

- 当前发布捕获的所有有效订阅均已完成、取消或失败。
- 错误策略已经执行。
- 临时 handler 已释放。
- 发布结果已经聚合。

`PublishAsync` 不代表：

- 等待由 handler 后续 `PostAsync` 的子事件。
- 等待其他 channel 中无关联事件。
- 等待事件导致的 State 后续持久化。

#### PostAsync

`PostAsync` 是受管排队方式。

完成条件：

- EventBus 接受事件并写入指定有界 channel。
- EventPostResult 返回事件 Id 和接受结果。

它不等待 handler 完成，但保证：

- 事件受 EventBus 生命周期管理。
- 消费失败可诊断。
- EventBus 停止时事件按关闭策略取消或 drain。
- 不产生未观察 Task。

### 3. 禁止同步阻塞发布

第一版不提供同步 `Publish`：

```text
Publish(...)
PublishAndWait(...)
```

原因：

- UI Thread 同步等待 UI handler 会死锁。
- 当前线程 handler 和异步 handler 组合语义复杂。
- 插件 handler 可能执行 IO。
- 同步异常传播会破坏 handler 隔离。

调用方必须使用 `await PublishAsync(...)` 或 `await PostAsync(...)`。

### 4. DispatchPolicy

每个订阅声明 EventBus `EventDispatchPolicy`。

#### Current

- Handler 在当前 EventBus delivery worker 执行上下文运行；它不承诺回到调用 `PublishAsync` 的线程。
- Channel worker 不继承首次创建 runtime 的 `ExecutionContext`，避免把某个 publisher 的 `AsyncLocal` 状态泄漏给后续 publication。
- 只适合轻量、无阻塞、无需线程切换的框架内部 handler。
- 不允许执行长耗时 IO。
- 发布方会直接承受 handler 延迟。

#### UiThread

- 通过 `IUiDispatcher` 投递。
- Presentation 未 ready 时按配置等待或拒绝。
- Dispatcher 已停止时拒绝投递。
- Handler 异常进入 EventBus error policy。
- 默认使用异步 `Post` 语义避免重入。

#### Background

- 通过可注入的 `IEventBackgroundScheduler` 运行。
- 绑定订阅 owner Scope。
- Handler 异常被观察。
- 不允许使用裸 `Task.Run` 逃逸生命周期。

#### Serialized

- 在订阅或指定 key 的串行队列中运行。
- 保证一次只有一个 handler 执行。
- 默认用于非 UI 应用 handler。

### 5. InlineIfAllowed 与 Post

`EventDispatchMode`：

| Mode | 语义 |
|---|---|
| `InlineIfAllowed` | 已处于目标执行上下文时可以立即调用。 |
| `Post` | 总是异步排队，避免调用栈重入。 |

默认：

- UI handler 使用 `Post`。
- Serialized handler 使用队列。
- Current handler 可以 `InlineIfAllowed`。
- 框架生命周期事件可按明确规则使用 inline。

使用 inline 必须保证 handler 不反向修改当前正在遍历的运行时状态。

### 6. Channel Runtime

每个 channel runtime 至少包含：

- EventContractId。
- Channel name。
- Execution mode。
- Capacity。
- BackpressurePolicy。
- Queue。
- Worker 状态。
- Partition manager。
- Subscription snapshot。
- Shutdown token。
- Diagnostics counters。

Channel runtime 由 ApplicationScope 或插件私有 runtime context 持有。

### 7. Serialized 模式

`Serialized` 是默认模式。

语义：

```text
Event 1 accepted
-> deliver Event 1 to its subscription snapshot
-> wait according to publication mode
-> Event 2 begins
```

需要区分：

- Channel serial：同一 channel 的多个事件串行。
- Subscription serial：同一订阅的多个 delivery 串行。

默认两者都串行。这样系统级状态通知的顺序最稳定。

### 8. Partitioned 模式

`Partitioned` 根据稳定 partition key 分组：

```text
Partition A: Event 1 -> Event 3 -> Event 7
Partition B: Event 2 -> Event 4
```

保证：

- 同 key 内按接受顺序执行。
- 不同 key 可以并行。
- 同时 active 的 partition 数量不超过 `MaximumConcurrency`。

要求：

- 发布方提供稳定、不可变 partition key。
- Partition key 不能引用插件私有对象。
- Partition 数量必须受控。
- 空闲 partition worker 必须回收。

1.0 runtime 不为 key 建立永久 worker 或永久字典项：只记录当前 in-flight key，并在该 delivery 结束时删除。因此 active partition 上限由 `MaximumConcurrency` 控制，等待中的不同 key 数量由 `Capacity` 控制，空闲 key 不残留资源。队首 key 正在运行时，调度器必须继续寻找其他可运行 key；新 key 入队和 active key 完成都会唤醒分区调度条件，不能产生队首阻塞或丢失唤醒。

适合多个独立文档、会话或资源实例的事件处理。

### 9. Concurrent 模式

`Concurrent` 允许事件和 handler 并发执行。

只适合：

- Handler 无共享可变状态。
- 不要求事件顺序。
- 不直接访问 UI。
- 有明确最大并发度。

Concurrent 仍然必须有：

- Channel capacity。
- Maximum concurrency。
- Scope cancellation。
- Error aggregation。
- Plugin drain。

Concurrent 不等于每个事件直接 `Task.Run`。

### 10. 订阅并发

Channel mode 和 subscription concurrency 是两个层次。

例如：

- Channel 可以 Partitioned。
- 某个订阅仍然要求全局 Serial。
- 另一个订阅允许按 partition 并行。

EventBus 根据两者生成 dispatch plan。

默认：

- Subscription `Serial`。
- Channel `Serialized`。

只有显式配置才放宽。

### 11. 发布快照

发布开始时获取不可变订阅快照：

```text
Read snapshot reference
-> Filter by channel and capability metadata
-> Create delivery plan
-> Execute without registry write lock
```

Snapshot 特性：

- 数组或等价紧凑不可变结构。
- 发布读取 O(1) 获取引用。
- 注册/撤销时复制并原子替换。
- 快照元素指向 subscription runtime，而不是直接裸 delegate。
- 执行前检查 subscription state。

Snapshot 允许发布与注册/撤销并发，但不能绕过 Quiescing barrier。

### 12. Handler 执行策略

同一事件的多个订阅默认可以独立调度，但发布结果需要等待它们完成。

单个 publication 同时启动的 delivery 数量由 `EventBusDispatchOptions.MaximumConcurrentDeliveriesPerPublication` 限制，默认 `16`，合法范围为 `1..1024`。该限制与 channel 的 publication 并发上限是两个不同层次。

默认不要求 handler 逐个串行完成，因为：

- 不同订阅之间没有业务顺序。
- UI 和后台 handler 的调度目标不同。

但必须受以下限制：

- 每个 subscription 自己的 serial policy。
- EventBus 最大并发度。
- Channel 背压。
- Plugin lifecycle barrier。

如果 Host 希望整个 channel 每次只处理一个 handler，可以显式选择严格串行策略。

### 13. 背压策略

所有 queue 都必须有有限 capacity。

#### Wait

- 等待队列出现容量。
- 等待可取消。
- 默认用于不能丢失的系统事件。
- UI Thread 调用时应优先使用 `PublishAsync` 或短 timeout，避免长时间阻塞。

#### Reject

- 队列满时立即返回 rejected。
- 适合调用方可以重试或降级的事件。

#### DropOldest

- 移除最早未开始事件。
- 必须记录 dropped EventId。
- 不适合状态转换和审计事件。

#### DropNewest

- 拒绝当前事件。
- 保留已在队列中的事件。

#### CoalesceLatest

- 相同 EventContractId、channel 和 partition key 只保留最新事件。
- 被替换事件记录为 coalesced。
- 适合刷新提示，不适合业务事实。

### 14. 背压默认值

框架不使用一个全局 capacity 适配所有 channel。

默认配置来源：

- EventBus global options。
- Event contract descriptor。
- Channel descriptor。
- Plugin capability policy。

规则：

- Capacity 必须大于零。
- 可靠系统 channel 默认 `Wait`。
- 插件私有 channel 必须有 Host 允许的上限。
- 插件不能创建无限 capacity。
- Drop/Coalesce 必须显式声明。

当前 1.0 public fallback 使用 `EventChannelOptions`：`Capacity=256`、`BackpressurePolicy=Wait`、`ExecutionMode=Serialized`、`MaximumConcurrency=1`、`QueueWaitTimeout=null`。`AddEventBus(EventChannelOptions)` 在 DI 配置阶段设置 fallback；`ConfigureEventChannel<TEvent>` 可为精确默认或命名 channel 设置 override；直接构造 `InMemoryEventBus` 时可传入同样的 fallback 和 descriptor 集合。重复的精确 type/channel descriptor 必须拒绝。每个精确 event contract + ordinal channel name 创建自己的 runtime，因而同一 identity 的 PublishAsync 与 PostAsync 共享 admission 和顺序，但不同 identity 不被错误串成一个全局队列。generated descriptor 后续接入同一配置入口，不能创建无界队列。

单条队列有界并不足以保证 EventBus 整体有界。`EventBusRuntimeOptions.MaximumChannelRuntimes` 同时限制一个 EventBus 实例能够持有的精确 runtime 数量，默认 256，合法范围为 1..65536。已有 runtime 的查找、上限检查和新 runtime 提交必须在同一锁内完成。达到上限时不得驱逐正在使用的 runtime：`PublishAsync` 明确抛 `EventPublicationRejectedException`，`PostAsync` 返回 rejected result，并写 `EventRejected`；已有 identity 继续可用。配置期精确 channel descriptor 的数量已经超过上限时，EventBus 必须在构造阶段失败。动态业务实体 id 不应直接作为 channel name；此类隔离应优先使用有界 partition key。

`Wait` 在容量释放、调用方取消、EventBus shutdown 或 `QueueWaitTimeout` 到期四者中竞争；timeout 产生明确 TimedOut rejection。`Reject` 和 `DropNewest` 不等待；`DropOldest` 只能替换尚未开始的最老 publication，不能取消已经 in-flight 的 handler；`CoalesceLatest` 只替换同一 partition key（非 Partitioned 时均为 null）最新的 pending publication。

`PublishAsync` 和 `PostAsync` 都不得绕过 runtime 直接调用 delivery engine。前者在成功 admission 后等待 publication completion；后者仅等待 admission。`DropOldest`/`CoalesceLatest` 替换已经 Accepted 的 Post 时必须写 `EventDropped`；如果被替换的是仍在等待结果的 Publish，则以 `EventPublicationRejectedException` 完成该 Publish，不能静默悬挂。

同一 runtime handler 内再次 `await PublishAsync` 到自身 channel 一律拒绝，避免 Serialized、满载 Partitioned 或满载 Concurrent 形成自等待；需要递归触发时使用 `PostAsync`（仍受 capacity/policy 约束）或显式选择另一个 channel。

### 15. UI Thread 约束

UI handler：

- 必须使用 `UiThread`。
- 默认异步 Post。
- 不允许在 handler 内同步等待另一个 UI 投递。
- 不允许执行长耗时 IO。
- 需要耗时任务时创建 Operation，再返回 UI 更新状态。

从 UI Thread 调用 `PublishAsync` 时：

- EventBus 不应阻塞线程。
- `await` 允许 Dispatcher 继续处理消息。
- Handler continuation 不强制回到 publisher UI context，除非调用方自己需要。

### 16. Cancellation

Delivery token 组合：

```text
EventBus shutdown token
+ Channel shutdown token
+ Subscription owner token
+ Plugin deactivation token
+ Publisher token
```

规则：

- 发布方取消不应撤销已完成 handler。
- Owner 停止必须取消未开始和执行中的 delivery。
- Plugin deactivation 优先于普通 publisher cancellation。
- Queue wait 必须可取消。
- 取消结果与失败结果分开统计。

### 17. Timeout

可以配置：

- Queue wait timeout。
- Handler execution timeout。
- Subscription drain timeout。
- EventBus shutdown timeout。

当前 1.0 `EventSubscriptionOptions` 的 handler execution timeout 默认为 `30s`；可以为单条订阅设置其他正值，或显式设为 `null` 表示不建立 handler deadline。允许的非 null 上限为 `Int32.MaxValue ms`。

Timeout 不等于强制终止 handler。

超时后：

- 触发 cancellation。
- 标记 timeout 诊断。
- 根据错误策略继续 drain、隔离订阅或进入 Plugin `UnloadPending`。
- `PublishAsync` 返回 `TimedOut` delivery；如果 handler 忽略 cancellation，订阅仍保持真实 in-flight 计数和串行门，直到 handler 实际退出，`StopAsync`/卸载不得谎报已经 drain。

### 18. 递归发布与重入

Handler 中可以调用 `PublishAsync`。

风险：

- Serialized channel 自己等待自己形成死锁。
- A -> B -> A 形成事件循环。
- UI handler 递归进入 UI 事件。

规则：

- EventContext 记录 publish depth 和 causation chain。
- 同一个 Serialized channel 的 handler 再次调用并等待该 channel 的 `PublishAsync` 时，EventBus 必须拒绝并返回 reentrant publication 错误，避免 channel 自等待。
- 超过最大 depth 时拒绝。
- UI handler 默认 Post，避免直接重入。
- 需要把同 channel 子事件排到当前事件之后时使用 `PostAsync`。

EventBus 必须输出循环诊断，而不是只表现为超时。

### 19. 错误聚合

`PublishAsync` 聚合每个 delivery 的结果：

```text
Succeeded
Cancelled
Failed
Rejected
Dropped
TimedOut
Skipped
```

`EventPublishResult` 至少包含：

- EventId。
- ContractId。
- Subscription count。
- Success count。
- Cancel count。
- Failure count。
- Duration。
- Handler result summaries。

默认不把完整 Exception 集合跨插件边界直接暴露给插件。插件私有异常应转换为稳定错误信息和诊断引用。

### 20. 性能结构

发布热路径建议：

```text
Resolve generated contract descriptor
-> Resolve channel runtime
-> Read immutable subscription snapshot
-> Rent/construct compact delivery state
-> Dispatch using prebuilt plans
-> Aggregate completion
```

优化方向：

- 避免 payload 装箱。
- 避免 LINQ 热路径。
- 避免反射。
- 避免每次发布复制订阅列表。
- 避免不必要 ExecutionContext capture。
- 合理使用 `ValueTask`。
- 对同步完成 handler 走快速路径。
- 对诊断 payload 使用采样和延迟格式化。

不能为了性能：

- 使用无界队列。
- 跳过 cancellation。
- 跳过插件 owner 检查。
- 把 handler 异常变成未观察 Task。
- 使用无法清理的静态泛型缓存。

### 21. Benchmark 规划

Benchmark 至少覆盖：

- 无订阅发布。
- 单同步完成 handler。
- 多同步完成 handler。
- UI dispatch 模拟。
- Serialized queue。
- Partitioned queue。
- Concurrent handler。
- 注册/撤销与发布竞争。
- 插件批量撤销。
- 诊断开启/关闭。
- channel identity 从 1 增长到 runtime 上限及超限拒绝。

关注指标：

- Throughput。
- P50/P95/P99 latency。
- Allocations per publication。
- Queue depth。
- Drain time。
- Snapshot replacement cost。

第一版不在架构文档中承诺未经验证的固定性能数字。

### 22. 测试要求

必须使用 deterministic dispatcher 测试：

- Serialized 顺序。
- Partition 内顺序。
- Partition 间并发。
- Concurrent 最大并发度。
- UI dispatch target。
- PublishAsync 完成语义。
- PostAsync 接受语义。
- 每种背压策略。
- 取消和 timeout。
- 递归发布检测。
- Quiescing barrier。
- EventBus shutdown drain。

### 23. 第一版明确决策

- 默认 channel Serialized。
- 默认 subscription Serial。
- 默认可靠事件使用 Wait。
- 所有队列有界。
- 不提供同步发布。
- 不提供全局顺序。
- 不使用无界 `Task.Run`。
- 高并发和丢弃必须显式开启。
