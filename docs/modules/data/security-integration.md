# AtomUI.City.Data Security Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Security Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。
- 授权失败返回明确 result，不能直接操作 UI。
- 权限声明必须来自 registry 或 plugin capability。
- 认证状态变更必须通知 command、route 和 data 集成点。

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

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Security Integration 设计

适用范围：Security credential、AccessTokenProvider、401/403 状态映射、用户切换、长连接认证和插件凭据边界。

Data 已完成请求前 credential 获取、匿名跳过、状态映射、异常隔离、长连接 principal switch 及插件 capability/revoke。refresh 的触发、并发合并和认证状态发布由具体 Security provider 或应用认证编排器负责；Data 在每次 operation 或连接重建时重新取 credential，但不会因 transport 返回 401 而自动刷新并重放当前 operation。

### 1. 定位

Data 通过 Security 获取认证凭据。

Data 不直接读取 token 存储，不管理登录态，不解释权限策略。Data 只根据请求 metadata 获取 credential，并把认证或授权失败映射为 DataError。

### 2. Credential 获取

```text
Data request
-> auth metadata
-> IAccessTokenProvider
-> credential result
-> attach credential
-> transport send
```

规则：

- credential 获取必须异步。
- credential 获取必须支持取消。
- Token 不写入日志。
- 匿名请求不强制获取 token。
- token provider 非取消异常必须映射为 `Unavailable` credential result，不能泄漏异常到 transport。
- 插件不能直接读取 Host token。

### 3. Refresh 边界

Data 只在进入 transport 前调用一次 `IAccessTokenProvider`。具体 provider 可以在这次调用内部刷新 token，并自行实现 single-flight；这不是 Data pipeline 的能力。

```text
Data operation
-> IAccessTokenProvider.GetTokenAsync
-> provider optionally refreshes under its own contract
-> Data attaches returned credential
-> transport executes once
-> transport 401 maps to AuthenticationRequired
```

规则：

- refresh 是否发生、是否合并并发调用由具体 provider 明确。
- provider 返回 `Expired` 或 `Required` 时，Data 在 transport 前分别返回 `AuthenticationExpired` 或 `AuthenticationRequired`。
- transport 返回 401 时，Data 返回 `AuthenticationRequired`，不在同一 operation 中刷新或重试。
- 应用完成 refresh 后，后续 operation 会重新向 provider 获取 credential。

### 4. 401 / 403

| 状态 | 语义 | 默认处理 |
|---|---|---|
| 401 / Unauthenticated | transport 拒绝当前凭据，认证失效或需要登录。 | AuthenticationRequired；由应用认证编排器决定后续 refresh 或 challenge。 |
| 403 / PermissionDenied | 已认证但权限不足。 | AuthorizationForbidden，不自动重试。 |

Data 对 401 和 403 都不自动 refresh。具体 provider 或应用可以在 Data operation 之外执行认证恢复。

### 5. 长连接认证

gRPC streaming 和 SignalR connection 需要特殊处理。

规则：

- token 过期后可能需要结束并重建 stream / connection。
- 用户切换账号时旧连接必须关闭。
- provider refresh 期间是否暂停业务发送由应用连接策略决定，Data 不自动协调。
- reconnect 后是否重新订阅由 subscription policy 决定。

### 6. 插件凭据边界

插件 Data client 使用 credential 必须走 Security/Data contract。

禁止：

- 插件读取 Host token store。
- 插件缓存 Host token。
- 插件把 token 写入日志。
- Host 静态缓存插件 credential callback。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| credential provider 缺失、失败或 unavailable | CredentialUnavailable。 |
| credential provider 明确返回 required | AuthenticationRequired。 |
| credential provider 返回 expired / required | AuthenticationExpired / AuthenticationRequired。 |
| transport 返回 401 | AuthenticationRequired，不自动重放当前 operation。 |
| 403 | AuthorizationForbidden。 |
| 用户切换 | 取消旧用户相关 operations 和 connections。 |
| 插件 credential callback 撤销 | PluginUnavailable。 |

### 8. 测试策略

当前测试覆盖 credential 注入、匿名跳过、状态映射、provider failure、plugin capability 和 principal switch；认证集成还必须遵守：

- credential 注入。
- 匿名请求不取 token。
- provider 的 expired / required 状态在 transport 前稳定映射。
- transport 401 映射为 AuthenticationRequired，且 Data 不自动 refresh 或重放。
- transport 403 映射为 AuthorizationForbidden，且 Data 不自动 refresh。
- 用户切换关闭连接。
- 插件不能读取 Host token。
