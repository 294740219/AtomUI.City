# AtomUI.City.Data Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 每个长连接必须声明 DataConnectionOwner。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 请求取消后不得写入 State、缓存或 UI。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 认证在 transport 执行前完成。 | 至少一个明确测试断言，不能只断言流程成功。 |
| HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-DATA-001 | Unit | DataPipelineTests | 断言执行顺序、取消不写缓存、retry diagnostics 和 transport exception retry。 | 取消、credential failed、cache failed、missing transport、timeout、transport exception。 | Completed |
| AUC-DATA-002 | Unit | HttpDataTransportTests | 断言 status -> DataErrorKind 映射，包含 validation failed 和 gateway timeout。 | 非成功状态码、validation failed、timeout、cancel。 | Completed |
| AUC-DATA-003 | Unit | GrpcDataTransportTests | 断言 GrpcStatusCode 标准数值和完整 status -> DataErrorKind 映射。 | status error、deadline、cancel、resource exhausted、precondition failed、data loss。 | Completed |
| AUC-DATA-004 | Unit | SignalRDataTransportTests | 断言 invocation context、connection closed 和 reconnect failed。 | connection closed、reconnect failed、invoke failed、timeout、cancel。 | Completed |
| AUC-DATA-005 | RuntimeLifecycle | DataConnectionLifecycleTests | 断言状态转换、owner 释放、重复 stop 幂等。 | owner dispose、重复 stop、start failed、stop failed。 | Completed |
| AUC-DATA-006 | Unit | AccessTokenCredentialProviderTests | 断言 credential before transport、status 映射、provider failure 映射。 | token missing、token failed、token provider exception。 | Completed |
| AUC-DATA-007 | Unit | DataRequestCacheTests | 断言 key 组成和 hit/miss。 | read failed、write failed。 | Required |
| AUC-DATA-008 | Unit | DataResultTests; DataDiagnosticsTests | 断言 result 不混用 success/error。 | unknown exception、cancelled、timeout。 | Required |
| AUC-DATA-009 | Unit | DataRegistrationTests | 断言默认服务。 | 重复注册、override。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
