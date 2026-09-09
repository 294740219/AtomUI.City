# AtomUI.City.Data Connection Lifecycle 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Connection Lifecycle` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-010 | Host Lifecycle Integration | DataHostIntegrationTests |
| AUC-DATA-011 | Native gRPC and Streaming | DataStreamingTests; DataDogfoodTests |
| AUC-DATA-012 | SignalR Realtime Connection | DataDogfoodTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Connection Lifecycle 设计

适用范围：HTTP、gRPC channel、gRPC streaming、SignalR connection、连接 owner、重连、关闭和插件卸载。

### 1. 定位

连接生命周期必须显式声明。

HTTP 通常是单次 Operation；gRPC channel、gRPC streaming 和 SignalR connection 可能跨越多个请求或持续运行。Data 必须明确连接挂在哪个生命周期边界下。

### 2. 生命周期选项

连接 owner 可以是：

```text
Application
Window
Navigation
Route
Activation
Plugin
Manual
```

规则：

- owner 是逻辑标签，不会自动绑定或创建 Core `LifecycleScope`。
- SignalR connection 必须声明 owner。
- gRPC channel 可以由 client factory 管理，但使用方必须声明关闭策略。
- Plugin owner 停止时必须关闭插件连接。
- Manual owner 需要调用方持有并 dispose manager 返回的 registration；Host shutdown 仍会回收尚未撤销的 registration。
- 已经处于 `Stopped` 的连接再次 stop 必须幂等，不能重复释放底层资源或重复调用连接 callback。

### 3. HTTP

HTTP 使用 `HttpClientFactory` 管理底层 handler lifetime。

规则：

- 业务请求仍然是独立 `DataRequestContext` operation，并可选绑定 `ParentScope`。
- Data 不手工长期持有裸 `HttpClientHandler`。
- named/typed client 配置通过 descriptor 或 DI 完成。

### 4. gRPC Channel

gRPC channel 生命周期可以长于单次 call。

规则：

- channel owner 必须明确。
- unary call 绑定调用方 cancellation/deadline；通过 pipeline 时由 request operation 管理。
- streaming call 使用 call cancellation；`DataStreamOptions.ParentScope` 可选绑定 Core lifecycle cancellation。
- deadline 和 cancellation 必须传入 call options。
- channel fault 进入 connection diagnostics。

### 5. SignalR Connection

SignalR connection 是显式长连接。

规则：

- start/stop 必须受 owner scope 管理。
- reconnect 策略显式配置。
- 默认不假设自动重连。
- token 变化、用户切换、插件停用必须关闭或重建连接。
- server handler 订阅必须可撤销。

### 6. 关闭顺序

Data 1.0 的 manager/transport 关闭流程如下；manager 负责注册与逆序 stop，具体 connection 实现负责其 subscription 和底层资源：

```text
Stop accepting new operations
-> snapshot connections in reverse registration order
-> invoke each connection StopAsync outside manager locks
-> SignalR connection rejects/revokes subscriptions and stops transport
-> remove successfully terminated registrations
-> emit diagnostics
```

插件连接必须保证没有 callback 持有插件私有类型。
如果 owner stop 被重复调用，已经停止的连接必须跳过后续 stop 流程。
同一个 connection id 只能注册一次；manager 返回的 `DataConnectionRegistration` 是可异步撤销句柄。
同一连接的外部并发 start/stop 必须共享已发布的事务 Task，用户 callback 在锁外执行；创建共享事务的首个调用者 token 驱动底层 start/stop，后续调用者 token 只取消自己的等待，不能替换已经发布的事务 token。同一异步调用链重入必须快速失败，不能等待自身事务。一项 stop 失败必须继续关闭同批次其他连接，最后保留单异常或聚合多异常；registration revoke 失败后允许再次调用重试。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| owner 已停止还创建连接 | 拒绝并记录诊断。 |
| reconnect 失败 | ReconnectFailed。 |
| stop 被调用方取消 | 停止后续连接；传播取消，保留尚未终止的 registration 以便重试。 |
| 插件连接停止失败 | 继续其他清理，最终由 contribution revoke 聚合异常。 |

### 8. 测试策略

当前测试覆盖 owner stop、并发事务、重入、逆序关闭、失败回滚/隔离、registration revoke、gRPC channel 和 SignalR reconnect/principal switch：

- Route owner 停止关闭连接。
- Plugin owner 停止关闭连接。
- SignalR reconnect failed。
- gRPC stream owner 取消。
- Manual owner registration revoke 与 Host shutdown 回收。
