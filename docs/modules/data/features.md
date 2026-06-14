# AtomUI.City.Data Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-DATA-001 | Request Pipeline | Completed | IDataRequestPipeline, DataRequestPipeline | DataPipelineTests |
| AUC-DATA-002 | HTTP Transport | Completed | HttpDataRequest<T>, HttpDataTransport | HttpDataTransportTests |
| AUC-DATA-003 | gRPC Transport | Ready to Start Product Implementation | GrpcDataRequest<T>, GrpcDataTransport | GrpcDataTransportTests |
| AUC-DATA-004 | SignalR Transport | Ready to Start Product Implementation | SignalRDataRequest<T>, SignalRDataTransport | SignalRDataTransportTests |
| AUC-DATA-005 | Connection Lifecycle | Ready to Start Product Implementation | DataConnectionManager, IDataConnection | DataConnectionLifecycleTests |
| AUC-DATA-006 | Authentication | Ready to Start Product Implementation | IDataCredentialProvider, AccessTokenCredentialProvider | AccessTokenCredentialProviderTests |
| AUC-DATA-007 | Caching | Ready to Start Product Implementation | IDataRequestCache, DataCacheKey | DataRequestCacheTests |
| AUC-DATA-008 | Error Model | Ready to Start Product Implementation | DataResult<T>, DataError | DataResultTests; DataDiagnosticsTests |
| AUC-DATA-009 | DI Registration | Ready to Start Product Implementation | DataServiceCollectionExtensions | DataRegistrationTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 每个长连接必须声明 DataConnectionOwner。 | 必须有实现、测试或工程门禁证据。 |
| 请求取消后不得写入 State、缓存或 UI。 | 必须有实现、测试或工程门禁证据。 |
| 认证在 transport 执行前完成。 | 必须有实现、测试或工程门禁证据。 |
| HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。 | 必须有实现、测试或工程门禁证据。 |
| 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-DATA-001 Request Pipeline

Feature ID: `AUC-DATA-001`
Status: Completed
Goal: 请求上下文、credential、cache、transport、retry 和 result mapping。
Public Contract: IDataRequestPipeline, DataRequestPipeline
Runtime / Build Behavior: 请求上下文、credential、cache、transport、retry 和 result mapping；transport exception 必须映射为 TransportError 后参与 retry policy。
Failure Behavior: 取消、credential failed、cache failed、missing transport、timeout、transport exception。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataPipelineTests`。
Required Assertions: 断言执行顺序、取消不写缓存、retry diagnostics 和 transport exception retry。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-002 HTTP Transport

Feature ID: `AUC-DATA-002`
Status: Completed
Goal: HTTP request/response 到 DataResult 映射。
Public Contract: HttpDataRequest<T>, HttpDataTransport
Runtime / Build Behavior: HTTP request/response 到 DataResult 映射；401、403、404、409、422、429、503、504 和 5xx 必须映射为稳定 DataErrorKind。
Failure Behavior: 非成功状态码、validation failed、timeout、cancel。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `HttpDataTransportTests`。
Required Assertions: 断言 status -> DataErrorKind 映射，包含 validation failed 和 gateway timeout。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-003 gRPC Transport

Feature ID: `AUC-DATA-003`
Status: Ready to Start Product Implementation
Goal: gRPC status 到 DataResult 映射。
Public Contract: GrpcDataRequest<T>, GrpcDataTransport
Runtime / Build Behavior: gRPC status 到 DataResult 映射。
Failure Behavior: status error、deadline、cancel。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `GrpcDataTransportTests`。
Required Assertions: 断言 GrpcStatusCode 映射。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-004 SignalR Transport

Feature ID: `AUC-DATA-004`
Status: Ready to Start Product Implementation
Goal: SignalR invocation 和实时连接。
Public Contract: SignalRDataRequest<T>, SignalRDataTransport
Runtime / Build Behavior: SignalR invocation 和实时连接。
Failure Behavior: connection closed、invoke failed、cancel。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `SignalRDataTransportTests`。
Required Assertions: 断言 invocation context。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-005 Connection Lifecycle

Feature ID: `AUC-DATA-005`
Status: Ready to Start Product Implementation
Goal: 连接 owner、启动、停止、失败和释放。
Public Contract: DataConnectionManager, IDataConnection
Runtime / Build Behavior: 连接 owner、启动、停止、失败和释放。
Failure Behavior: owner dispose、重复 stop、start failed。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataConnectionLifecycleTests`。
Required Assertions: 断言状态转换、owner 释放。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-006 Authentication

Feature ID: `AUC-DATA-006`
Status: Ready to Start Product Implementation
Goal: 从 Security 获取 token 或 credential。
Public Contract: IDataCredentialProvider, AccessTokenCredentialProvider
Runtime / Build Behavior: 从 Security 获取 token 或 credential。
Failure Behavior: token missing、token failed。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `AccessTokenCredentialProviderTests`。
Required Assertions: 断言 credential before transport。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-007 Caching

Feature ID: `AUC-DATA-007`
Status: Ready to Start Product Implementation
Goal: 请求缓存 key、命中、失效和并发访问。
Public Contract: IDataRequestCache, DataCacheKey
Runtime / Build Behavior: 请求缓存 key、命中、失效和并发访问。
Failure Behavior: read failed、write failed。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataRequestCacheTests`。
Required Assertions: 断言 key 组成和 hit/miss。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-008 Error Model

Feature ID: `AUC-DATA-008`
Status: Ready to Start Product Implementation
Goal: DataResultStatus、DataErrorKind 和 mapper。
Public Contract: DataResult<T>, DataError
Runtime / Build Behavior: DataResultStatus、DataErrorKind 和 mapper。
Failure Behavior: unknown exception、cancelled、timeout。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataResultTests; DataDiagnosticsTests`。
Required Assertions: 断言 result 不混用 success/error。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-DATA-009 DI Registration

Feature ID: `AUC-DATA-009`
Status: Ready to Start Product Implementation
Goal: 默认 pipeline、factory、transport、diagnostics 注册。
Public Contract: DataServiceCollectionExtensions
Runtime / Build Behavior: 默认 pipeline、factory、transport、diagnostics 注册。
Failure Behavior: 重复注册、override。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `DataRegistrationTests`。
Required Assertions: 断言默认服务。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
