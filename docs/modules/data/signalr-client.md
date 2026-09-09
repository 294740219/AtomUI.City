# AtomUI.City.Data SignalR Client 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `SignalR Client` 相关实现决策，不重新定义模块边界。

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

- `DataConnectionOwnerKind` 只允许 Application、Window、Navigation、Route、Activation、Plugin 或 Manual。
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
| AUC-DATA-012 | SignalR Realtime Connection | DataDogfoodTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data SignalR Client 设计

适用范围：SignalR connection、hub method invoke、server push、subscription、reconnect、access token provider 和插件卸载。

### 1. 定位

SignalR 是 Data 第一批一等 realtime transport。

除显式 invoker 兼容 adapter 外，`SignalRRealtimeConnection` 已接入官方 `HubConnection`、server push、订阅撤销、automatic reconnect、principal switch 和 owner shutdown。

SignalR client 负责实时连接、服务端推送、hub method invoke 和双向消息。它是外部实时数据入口，不替代应用内部 EventBus。

### 2. Connection

SignalR connection 应注册到 `DataConnectionManager`，或由调用方提供等价且可证明的关闭路径。

规则：

- connection owner 必须显式声明。
- start/stop 必须可诊断。
- reconnect 策略必须显式配置。
- 默认不假设自动重连。
- connection state 必须可观察。
- owner 是逻辑标签；应用在对应生命周期 hook 调用 `StopOwnerAsync`，插件 connection 由 contribution lease 撤销。

### 3. Hub Invoke

Hub method invoke 是 request/response 操作。

```text
Hub invoke
-> caller cancellation / explicit credential source
-> SignalRRealtimeConnection
-> DataResult<T>
```

原生 invoke 接收 method name、arguments 和 cancellation token；连接创建时的 `AccessTokenProvider` 负责凭据。兼容 `SignalRDataTransport` 才使用 `SignalRInvocationContext`/`DataRequestContext`。invoke 失败必须映射为 DataError。

### 4. Server Push

Server push 必须通过 subscription 注册。

规则：

- handler 绑定 connection 持有的 `IDataSubscription`；connection stop/principal switch/dispose 时撤销。
- handler 不能直接访问 UI。
- handler 不能无限缓冲消息。
- handler 必须可撤销。
- 插件 handler 不能被 Host 静态缓存持有。

### 5. Token 和重连

SignalR access token 通过 Security 提供。

规则：

- AccessTokenProvider 每次需要 token 时从 Security 获取。
- token refresh 由应用提供的 callback/Security 在返回 token 前完成。
- 用户切换账号时调用 `SwitchPrincipalAsync`，它撤销旧 subscription，并在已连接时 stop/start transport。
- Data 不为 reconnect 期间的 hub invoke 提供消息排队。
- reconnect 失败返回 ReconnectFailed。

### 6. SignalR 与 EventBus

边界：

```text
SignalR receives server message
-> Data realtime transport
-> explicit mapper
-> State update or EventBus publish
```

SignalR 不直接广播到所有模块。是否转为 EventBus 事件必须显式声明。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| `SignalRConnectionClosedException` | ConnectionClosed。 |
| `SignalRReconnectFailedException` | ReconnectFailed。 |
| hub invoke failed | TransportError 或 ServerError。 |
| token provider 缺失、失败或 unavailable | CredentialUnavailable。 |
| token provider 明确返回 required | AuthenticationRequired。 |
| owner stopped | stop connection and subscriptions。 |

### 8. 测试策略

当前测试覆盖委托式 invocation、取消、lifecycle exception mapping，以及真实 HubConnection 的以下行为：

- connection start / stop。
- hub invoke success / failure。
- server push delivery。
- subscription disposal。
- reconnect failed。
- access-token callback 与 principal switch/restart。
- plugin unload stops connection。
