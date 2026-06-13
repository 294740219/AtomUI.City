# AtomUI.City.Data Plugin Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
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

## AtomUI.City.Data Plugin Integration 设计

适用范围：插件 Data client、capability、请求取消、连接停止、缓存撤销、contract 隔离和卸载诊断。

### 1. 定位

Plugin integration 负责约束插件如何贡献和使用 Data client。

插件可以贡献 HTTP/gRPC/SignalR client，但必须经过 Host 管理的 descriptor、capability、lifecycle 和 pipeline。

### 2. 插件可贡献内容

插件可以贡献：

- HTTP client descriptor。
- gRPC client descriptor。
- SignalR hub descriptor。
- Serializer metadata。
- Auth metadata。
- Cache metadata。
- Resilience metadata。
- Connection lifetime metadata。

### 3. Capability

插件使用 Data client 必须有 capability。

示例：

- UseHttpClient。
- UseGrpcClient。
- UseSignalRHub。
- UseDataClient。
- UseRealtimeConnection。

未授权 capability 的 client contribution 不得进入 registry。

### 4. 生命周期

插件停用时：

```text
Stop new plugin data operations
-> cancel running operations
-> stop streams and realtime connections
-> revoke client descriptors
-> invalidate plugin cache
-> clear callbacks
-> dispose contribution leases
```

### 5. Contract 隔离

跨插件边界 DTO、event、stream item contract 必须位于 Host 共享 contract 程序集。

禁止：

- Host 静态缓存插件私有 client 实例。
- Host 长期持有插件私有 callback。
- 插件读取 Host token store。
- 插件绕过 Data pipeline。
- 插件启动非受控 connection 或 background receive loop。

### 6. 长连接

插件 SignalR/gRPC streaming 连接必须绑定插件 owner。

插件停用必须：

- 停止新消息投递。
- 取消 stream。
- stop connection。
- 移除 handler。
- 确认没有插件类型引用残留。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| capability denied | contribution rejected。 |
| plugin client conflict | contribution rejected。 |
| plugin unload with active connection | cancel and stop；失败进入 UnloadPending。 |
| plugin cache revoke failed | 聚合错误，继续清理。 |

### 8. 测试策略

测试必须覆盖：

- 插件 HTTP/gRPC/SignalR client 注册。
- capability denied。
- 插件停用取消请求。
- 插件停用关闭 SignalR connection。
- 插件 cache revoke。
- Host 不持有插件私有类型。
