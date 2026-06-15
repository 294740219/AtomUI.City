# AtomUI.City.Data SignalR CLIent 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `SignalR CLIent` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Data SignalR Client 设计

适用范围：SignalR connection、hub method invoke、server push、subscription、reconnect、access token provider 和插件卸载。

### 1. 定位

SignalR 是 Data 第一批一等 realtime transport。

SignalR client 负责实时连接、服务端推送、hub method invoke 和双向消息。它是外部实时数据入口，不替代应用内部 EventBus。

### 2. Connection

SignalR connection 必须由 `IDataConnectionManager` 或等价能力管理。

规则：

- connection owner 必须显式声明。
- start/stop 必须可诊断。
- reconnect 策略必须显式配置。
- 默认不假设自动重连。
- connection state 必须可观察。
- owner scope 停止时必须 stop connection。

### 3. Hub Invoke

Hub method invoke 是 request/response 操作。

```text
Hub invoke
-> OperationScope
-> Security credential
-> SignalR transport
-> DataResult<T>
```

invoke context 必须携带 hub name、method name、DataRequestContext、credential 和 cancellation token。invoke 失败必须映射为 DataError。

### 4. Server Push

Server push 必须通过 subscription 注册。

规则：

- handler 绑定 SubscriptionScope。
- handler 不能直接访问 UI。
- handler 不能无限缓冲消息。
- handler 必须可撤销。
- 插件 handler 不能被 Host 静态缓存持有。

### 5. Token 和重连

SignalR access token 通过 Security 提供。

规则：

- AccessTokenProvider 每次需要 token 时从 Security 获取。
- token 过期时可触发 refresh。
- 用户切换账号时旧 connection 必须关闭并重建。
- reconnect 期间是否排队消息由 policy 决定。
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
| token unavailable | AuthenticationRequired。 |
| owner stopped | stop connection and subscriptions。 |

### 8. 测试策略

测试必须覆盖：

- connection start / stop。
- hub invoke success / failure。
- server push delivery。
- subscription disposal。
- reconnect failed。
- token refresh。
- plugin unload stops connection。
