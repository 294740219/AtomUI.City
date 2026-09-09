# AtomUI.City.Data Headless Dogfood

## 目标

在无 GUI、无外部服务依赖的单进程应用中启动真实本地服务，验证 Data 1.0 的公开合同能够组合运行，而不只依赖 mocked unit test。

## 拓扑

```text
Data.HeadlessApp
|- HTTP/1.1 Kestrel endpoint
|- HTTP/2 gRPC endpoint
|- SignalR WebSocket hub
|- DataRequestPipeline and large-payload client
|- NativeGrpcClient unary/server/client/bidi streams
`- SignalRRealtimeConnection invoke/push/reconnect/principal switch
```

服务端和客户端共享同一 Host 进程，但经过真实 loopback socket、HTTP/2 和 WebSocket 协议栈。测试不访问公网。

## 场景

1. 并发执行 100 个 HTTP pipeline 请求并验证结果隔离。
2. 使用固定缓冲区上传和下载 256 KiB payload。
3. 执行 100 个 gRPC unary 请求，并覆盖 metadata、deadline 和 credential。
4. 完整执行 gRPC server、client 和 bidirectional streaming。
5. 执行 100 个 SignalR invoke，并验证 server push subscription。
6. 强制中断 WebSocket，验证 `Connected -> Reconnecting -> Connected`。
7. 切换 principal revision，验证旧订阅撤销和连接重启。
8. 在 subscription handler 内调用 `StopAsync`，验证重入不死锁。
9. 按 owner 关闭全部长期连接。
10. 装载 generator 生成的 descriptor registrar。

## 通过条件

- 进程退出码为 0。
- 标准输出包含 `DATA_HEADLESS_OK`。
- 任一断言、协议调用、生命周期清理或超时失败均返回非零退出码。

NativeAOT 路径由相邻的 `AtomUI.City.Data.AotApp` 验证；它发布为原生可执行文件并输出 `DATA_AOT_OK`。
