# AtomUI.City.Data Lifecycle

## 生命周期范围

执行边界：Host runtime data pipeline。

AtomUI.City.Data 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- Request: Created -> Authenticating -> CacheLookup -> Sending -> Mapping -> Completed 或 Failed 或 Cancelled
- Connection: Created -> Connecting -> Connected -> Reconnecting / Disconnecting -> Stopped 或 Faulted
- Stream（AUC-DATA-011/012，Completed）: Subscribed -> Active -> Completing -> Completed 或 Cancelled 或 Faulted

## 生命周期流程

- DataRequest 进入 pipeline。
- Pipeline 创建 DataRequestContext 并获取 credential。
- Cache policy 决定读取、跳过或写入。
- Transport 执行 HTTP、gRPC 或 SignalR。
- DataErrorMapper 统一映射失败。

## Host Shutdown / 执行结束行为

- 使用 `DataModule` 时，Host 停止先关闭请求 runtime gate，再阻止新连接注册并停止已有连接。
- 取消未完成后台任务。
- connection manager 按全局注册逆序停止连接；插件 lease 在自身边界内先停连接，再等待请求 drain 并撤销 descriptor/client/cache。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- credential provider 缺失、失败或 unavailable：返回 `CredentialUnavailable`；明确 required 时返回 `AuthenticationRequired`；两者都不调用 transport。
- transport timeout：返回 Timeout，诊断包含 operationId；协议 endpoint 不属于 generated descriptor。
- `StopOwnerAsync` 关闭该 owner 的 connection；普通请求只有在绑定 `ParentScope`、plugin contribution 或 Host runtime gate 时才随对应边界取消。
- 请求取消：返回 Cancelled，不写缓存和状态。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
