# AtomUI.City Data 七模块联合实战方案

## 1. 目标

本方案在既有 StressCli 中加入 Data，通过真实环回 HTTP、gRPC 和 SignalR 服务验证以下纵向链：

```text
Router -> Mvvm command -> Data -> EventBus -> State -> Localization
       -> Core LifecycleScope / Host shutdown
```

Data.HeadlessApp 继续负责协议本身的完整 dogfood；本夹具负责证明 Data 进入复杂 City 应用后，认证、缓存、并发、状态投影、导航和释放仍保持确定性。

## 2. 规模

| 项目 | 目标 |
| --- | ---: |
| 业务 Module | 40（另含 EventBus、Routing、Data 框架模块） |
| 业务 Service | 67 |
| EventBus 契约 | 43 |
| State | 84（66 registry + 10 computed + 8 collection） |
| ViewModel | 17 |
| 静态路由 | 31 |
| Data operation | 4 个 generated descriptor + HTTP/gRPC/SignalR 运行时调用 |

## 3. 本地服务

每个 Data 阶段独占启动一个无外部依赖的 Kestrel 服务：

- HTTP/1.1：商品读取、订单提交、账号回显、延迟请求、瞬态失败和大载荷；
- HTTP/2 gRPC：库存 unary、价格 server stream、库存 duplex stream；
- SignalR：库存与配送推送、主动断线和自动重连；
- 服务端保留线程安全请求计数，并允许按 operation 注入延迟和指定次数失败。

服务先于 City Host 启动；City Host 必须先停止 Data 请求和连接，随后才释放本地服务。

## 4. 阶段

### Phase P - 业务纵向链

导航至远程运营路由，激活 RemoteOperationsViewModel，通过 HTTP 执行缓存查询和订单 mutation。成功结果发布远程业务事件并投影到 State；错误转为当前 culture 的本地化文案。离开路由必须取消延迟请求并禁止迟到结果写入。

### Phase Q - Realtime 与账号切换

gRPC stream 和 SignalR push 进入 EventBus，再更新 registry state 与 collection。切换 user-A/user-B 时必须更换 credential、失效旧 principal cache、重建连接，并拒绝旧账号消息污染当前 ViewModel。

### Phase R - 并发、混沌与停机

按 seed 并发执行缓存 query、mutation、LatestWins、KeyedSerial、retry、circuit、取消和 contribution revoke。最后在请求与连接仍活跃时停止 Host，验证拒绝新请求、drain、逆序关闭和零释放后回调。

## 5. 不变量

1. Generated catalog 只包含声明的客户端和四项 operation。
2. Bearer token 不进入日志、诊断、State 或事件。
3. 同 principal 重复 query 命中缓存；mutation 后下一次 query 必须访问服务端。
4. 乐观更新成功 confirm，失败或取消 rollback。
5. 每个成功业务结果只产生一次事件、State 更新和 ViewModel 反应。
6. 路由或 activation scope 停止后，迟到结果不得提交。
7. gRPC 与 SignalR 消息保持各自声明的顺序和背压语义。
8. principal 切换后旧缓存、旧连接和旧订阅不可见。
9. contribution revoke 后不得再执行其 handler 或访问其缓存。
10. Host stopping 后新请求失败，在途请求完成取消和 drain，所有连接进入终态。

## 6. 执行矩阵

```text
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net8.0 -- data-suite --profile quick
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net10.0 -- data-suite --profile quick
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net10.0 -- data-suite --profile standard
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net10.0 -- data-extreme --seed 20260909
```

任何框架缺陷必须先进入对应模块回归测试，再修改源码与合同文档；夹具不能通过绕过 public API 获得绿色结果。
