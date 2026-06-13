# AtomUI.City.Data Streaming And Realtime 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Streaming And Realtime` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。

## Public Contract

- 只允许通过 `AtomUI.City.Data` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-DATA-001 | Request Pipeline | DataPipelineTests |
| AUC-DATA-002 | HTTP Transport | HttpDataTransportTests |
| AUC-DATA-003 | gRPC Transport | GrpcDataTransportTests |
| AUC-DATA-004 | SignalR Transport | SignalRDataTransportTests |
| AUC-DATA-005 | Connection Lifecycle | DataConnectionLifecycleTests |
| AUC-DATA-006 | Authentication | AccessTokenCredentialProviderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Streaming and Realtime 设计

适用范围：gRPC streaming、SignalR server push、subscription、backpressure、状态投影和 EventBus 边界。

### 1. 定位

Streaming 和 realtime 是 Data 的一等访问模式。

它们不是普通请求的循环版。它们需要 subscription 生命周期、backpressure、连接状态、取消、重连和消息投影策略。

### 2. gRPC Streaming

支持：

- Server streaming。
- Client streaming。
- Bidirectional streaming。

规则：

- server stream item 必须经过 Data subscription dispatcher。
- client stream 写入必须响应 cancellation。
- bidi stream 必须同时管理读取和写入取消。
- deadline / cancellation 必须进入 call options。
- stream completed 是正常状态，不是错误。

### 3. SignalR Realtime

支持：

- Hub connection start / stop。
- Hub method invoke。
- Server event subscription。
- Reconnect policy。
- Connection state observable。
- Token refresh / reconnect。

SignalR 不替代 EventBus：

```text
SignalR receives server message
-> Data realtime transport
-> explicit mapper
-> State update or EventBus publish
```

SignalR 是外部实时数据入口。EventBus 是应用内部模块通信机制。

### 4. Backpressure

Streaming 和 realtime 必须声明 backpressure policy。

| 策略 | 说明 |
|---|---|
| `Buffer` | 有界缓冲，满了按策略处理。 |
| `DropOldest` | 丢旧消息，保留最新。 |
| `DropNewest` | 丢新消息，保护消费者。 |
| `LatestOnly` | 状态型消息只保留最后一条。 |
| `BlockProducer` | 能反压时阻塞生产方，SignalR 多数场景不适合。 |

默认不允许无限缓冲。

### 5. SubscriptionScope

每个 streaming subscription 或 SignalR server handler 都必须绑定 Scope。

Scope 停止时：

- 停止接收新消息。
- 取消 stream。
- 释放 handler。
- 清理 buffer。
- 停止结果投递。

### 6. 状态投影

Streaming 和 SignalR 默认不缓存原始消息。

允许：

- latest snapshot。
- bounded buffer。
- 显式 State projection。
- 显式 EventBus publish。

禁止：

- 无限消息缓存。
- 隐式写全局 State。
- transport callback 直接改 ViewModel。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| stream cancel | StreamCancelled，不作为失败。 |
| stream completed | StreamCompleted。 |
| backpressure drop | 记录 drop diagnostics。 |
| handler 抛异常 | 记录错误，按 subscription error policy 处理。 |
| reconnect failed | ReconnectFailed。 |

### 8. 测试策略

测试必须覆盖：

- server streaming 正常完成。
- stream cancellation。
- SignalR server push。
- backpressure DropOldest。
- LatestOnly 只投递最新状态。
- subscription scope 停止后不再投递。
