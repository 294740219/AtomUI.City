# AtomUI.City.Data Error Model 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Error Model` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-007 | Caching | DataRequestCacheTests |
| AUC-DATA-008 | Error Model | DataResultTests; DataDiagnosticsTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Error Model 设计

适用范围：DataResult、DataError、HTTP/gRPC/SignalR 错误映射、取消语义和诊断。

### 1. 定位

Data 不应该把预期失败都作为异常抛给 ViewModel。

`DataResult<T>` 表达请求成功、失败、取消和部分结果。异常用于不可预期的框架错误，进入 DataError mapping 后返回调用方。

成功结果只能携带 value，不能携带 error。失败、取消和 stale suppression 结果只能携带 DataError，不能携带 value。调用方必须用 `Status` 或 `Succeeded` 判断结果，不能通过 value 是否为空推断成功。

### 2. DataResult

结果建议：

```text
Success
Failed
Cancelled
Partial
StaleSuppressed
```

`StaleSuppressed` 表示请求完成时 parent scope、operation sequence 或 plugin contribution 已失效，结果未提交。

### 3. DataError

建议错误类型：

```text
Cancelled
Timeout
NetworkUnavailable
CredentialUnavailable
AuthenticationRequired
AuthenticationExpired
AuthorizationForbidden
BadRequest
NotFound
Conflict
ValidationFailed
ServerError
ServiceUnavailable
TransportError
SerializationError
PolicyRejected
ConnectionFailed
ConnectionClosed
ReconnectFailed
StreamCancelled
StreamCompleted
StreamProtocolError
DeadlineExceeded
Unavailable
PluginUnavailable
LocalStorageError
Unknown
```

`DataErrorKind` 必须是已定义值；`DataError.Message` 必须是非空白字符串。`MessageKey` 和 `MessageArguments` 只承载本地化元数据，不改变错误分类。

### 4. Transport 映射

| 来源 | 映射 |
|---|---|
| HTTP 401 | AuthenticationRequired / AuthenticationExpired。 |
| HTTP 403 | AuthorizationForbidden。 |
| HTTP 422 | ValidationFailed。 |
| HTTP 504 | Timeout。 |
| gRPC Unauthenticated | AuthenticationRequired / AuthenticationExpired。 |
| gRPC PermissionDenied | AuthorizationForbidden。 |
| gRPC DeadlineExceeded | DeadlineExceeded / Timeout。 |
| gRPC InvalidArgument / OutOfRange | ValidationFailed。 |
| gRPC ResourceExhausted | PolicyRejected。 |
| gRPC FailedPrecondition / Aborted | Conflict。 |
| gRPC Unavailable | ServiceUnavailable。 |
| gRPC Unimplemented / Internal / DataLoss | ServerError。 |
| SignalR reconnect failed | ReconnectFailed。 |
| SignalR closed | ConnectionClosed。 |
| Scope cancellation | Cancelled。 |

### 5. 取消语义

取消不是错误。

取消来源：

- 用户取消。
- Scope 停止。
- Navigation 离开。
- ViewModel 停用。
- Plugin 停用。
- Host 关闭。
- `CancelPrevious` 并发策略。

取消必须可诊断，但不进入 fatal error。

### 6. 错误边界

DataError 不直接决定 UI 展示。

调用方可以映射到：

- Resolver `ResolveResult`。
- Command result。
- State update。
- Presentation notification。
- Diagnostics record。

### 7. 测试策略

测试必须覆盖：

- HTTP status 映射。
- gRPC status 映射。
- SignalR closed / reconnect failed。
- cancellation。
- stale result suppression。
- serialization error。
- plugin unavailable。
