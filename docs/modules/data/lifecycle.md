# AtomUI.City.Data Lifecycle

## 生命周期范围

执行边界：Host runtime data pipeline。

AtomUI.City.Data 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- Request: Created -> Authenticating -> CacheLookup -> Sending -> Mapping -> Completed 或 Failed 或 Cancelled
- Connection: Created -> Opening -> Open -> Closing -> Closed 或 Faulted
- Stream: Subscribed -> Active -> Completing -> Completed 或 Cancelled 或 Faulted

## 生命周期流程

- DataRequest 进入 pipeline。
- Pipeline 创建 DataRequestContext 并获取 credential。
- Cache policy 决定读取、跳过或写入。
- Transport 执行 HTTP、gRPC 或 SignalR。
- DataErrorMapper 统一映射失败。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- credential provider 不可用：返回 Unauthorized，不调用 transport。
- transport timeout：返回 Timeout，诊断包含 endpoint 和 operationId。
- connection owner dispose：取消未完成请求并关闭 connection。
- 请求取消：返回 Cancelled，不写缓存和状态。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
