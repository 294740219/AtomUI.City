# AtomUI.City.Data Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 每个长连接必须声明 DataConnectionOwner。
- 请求取消后不得写入 State、缓存或 UI。
- 认证在 transport 执行前完成。
- HTTP、gRPC、SignalR 统一映射到 DataResult 和 DataErrorKind。
- 缓存 key 必须包含 request identity、transport、endpoint、method、payload identity 和安全上下文相关部分。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `DataRequestPipeline` 的 credential -> cache -> transport 顺序、取消后不写缓存、transient result retry 和 transport exception retry diagnostics 进入 1.0 兼容承诺。
- `HttpDataTransport` 的非成功 status 映射进入 1.0 兼容承诺，特别是 422 -> `ValidationFailed`、504 -> `Timeout`。
- `GrpcStatusCode` 的枚举数值必须匹配 gRPC protocol 标准状态码；`GrpcDataTransport` 的 status -> `DataErrorKind` 映射进入 1.0 兼容承诺。
- `SignalRDataTransport` 的 invocation context 内容、`SignalRConnectionClosedException` -> `ConnectionClosed` 和 `SignalRReconnectFailedException` -> `ReconnectFailed` 映射进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
