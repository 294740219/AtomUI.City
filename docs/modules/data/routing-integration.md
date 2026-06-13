# AtomUI.City.Data Routing Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Routing Integration` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Data Routing Integration 设计

适用范围：Resolver 调用 Data、导航取消、ResolveResult 映射、预取、缓存和诊断。

### 1. 定位

Routing Resolver 可以调用 Data client，为页面进入准备首屏必需数据。

Resolver 不实现自己的请求管线，不直接访问 transport，不隐式写全局 State。

### 2. Resolver 链路

```text
Resolver
-> Data client
-> Data pipeline
-> DataResult
-> ResolveResult
-> RouteContext data
```

Data 请求必须接收 Resolver 的 `CancellationToken`。

### 3. 错误映射

| DataResult | ResolveResult |
|---|---|
| Success | Success(data)。 |
| Cancelled | Cancelled。 |
| NotFound | NotFound。 |
| AuthenticationRequired | Redirect / Challenge，按 route policy。 |
| AuthorizationForbidden | Failed 或 forbidden route，按 route policy。 |
| Timeout / NetworkUnavailable | Failed 或 retry route，按 Host 策略。 |

Routing 决定导航结果，Data 只提供标准错误。

### 4. 预取和缓存

Resolver 可以使用 Data cache。

规则：

- Resolver cache 必须绑定 RouteScope、NavigationScope 或 Data cache。
- 不使用无边界静态缓存。
- Journal 恢复时只能复用可序列化快照。
- Principal change 必须让受保护数据缓存失效。

### 5. 取消

导航取消时：

- Resolver cancellation token 取消。
- Data OperationScope 取消。
- transport 请求取消。
- 返回 ResolveResult Cancelled。
- 不提交 State。

### 6. 插件 Resolver

插件 Resolver 调用插件 Data client 时：

- client 来自插件 service context。
- operation 绑定插件 contribution。
- 插件停用取消请求。
- DTO 跨边界必须位于 Host 共享 contract 程序集。

### 7. 测试策略

测试必须覆盖：

- Resolver Data success。
- Data NotFound 映射。
- Data auth failure 映射。
- 导航取消取消 Data 请求。
- Resolver cache。
- 插件停用取消 Resolver Data 请求。
