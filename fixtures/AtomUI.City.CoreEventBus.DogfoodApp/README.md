# Core + EventBus Dogfood CLI 验收设计

## 1. 定位

`AtomUI.City.CoreEventBus.DogfoodApp` 是只使用 City Core 与 EventBus 生产能力的长期实战 fixture。它把两个已经独立验收的基础模块当作一个最小产品平台，模拟一个本地任务处理系统，用真实业务边界验证 Module、DI、生命周期和事件协作能否在复杂组合下稳定工作。

本项目不是 Core 或 EventBus 单元测试的替代品，也不是 `AtomUI.City.Cli` 模块的实现。它是可手工运行、可由 CI 判定、可 NativeAOT 发布的无 UI 控制台产品。

## 2. 约束

- 运行时 City 依赖只能是 `AtomUI.City.Core` 与 `AtomUI.City.EventBus`。
- Source Generator 仅作为编译期 Analyzer 引用。
- 不得使用运行时程序集扫描、反射式服务发现或独立 `BuildServiceProvider()`。
- 必须通过 `ApplicationHost`、ModuleCatalog、生成式 DI 和 EventBus Host 生命周期启动。
- 保留既有 Core/EventBus Headless fixture；本项目只增加联合产品证据。
- 顶层失败必须写入 `stderr` 并返回非零退出码，不得弹出需要人工关闭的 Windows 错误框。

## 3. 业务模型与模块图

应用模拟一个本地任务处理平台。用户在工作区提交任务，系统完成身份校验、排队、执行、审计、报告、通知和维护统计。

```text
DogfoodApplicationModule
  -> EventBusModule
  -> IdentityModule
  -> WorkspaceModule -> IdentityModule
  -> JobsModule -> WorkspaceModule
  -> ExecutionModule -> JobsModule
  -> AuditModule -> JobsModule
  -> ReportingModule -> JobsModule, AuditModule
  -> NotificationModule -> ExecutionModule
  -> MaintenanceModule -> ReportingModule, NotificationModule
```

模块图必须无环，并由 Host Build 自动验证。应用根及依赖闭包合计至少 10 个 Module（包含 EventBusModule）。

## 4. DI 设计

本 fixture 是单程序集。依照 Core 的所有权合同，一个程序集只能有一个 `[ServiceRegistrationOwner]`，因此生成式服务统一归 `DogfoodApplicationModule` 所有；业务 Module 仍负责依赖图和生命周期边界，不伪造多个 registrar owner。

服务矩阵至少包含：

- 30 个由 Attribute/source generator 注册的业务 Service；
- Singleton、Scoped、Transient 三种 lifetime；
- interface exposed contract 与具体实现；
- 构造函数形成多层但无环的依赖链；
- 从 Host Provider 创建显式 `IServiceScope`，跨两个 scope 验证 singleton/scoped/transient；生命周期 Scope 继续只承担 EventBus subscription owner，不把并不存在的 ServiceProvider API 写进合同；
- 未选 Module 的自动服务不得进入 Root，本 fixture 不通过额外 `BuildServiceProvider()` 绕过该规则。

## 5. EventBus 设计

至少定义 12 个强类型事件契约、20 个静态生成 Handler，覆盖：

- `commands`：Serialized，验证 Publish/Post 共享 admission 顺序；
- `jobs`：Partitioned，按 Workspace/Job key 保序并允许跨分区并发；
- `telemetry`：Concurrent，验证有界并发 fan-out；
- `failures`：Serialized，验证 ContinueAndReport、StopPublication 与 DisableSubscription；
- 动态订阅必须绑定 Operation Scope，owner 停止后只能释放该订阅线。

事件必须携带稳定业务 ID；跨模块协作优先使用合同事件，不能由 Handler 反向依赖具体发布方。

## 6. 场景与断言

CLI 支持以下命令；无参数等价于 `verify-all`：

| 场景 | 核心行为 | 必须断言 |
| --- | --- | --- |
| `happy-path` | 一条任务贯穿受理、执行、报告、通知 | Module/Service/Contract/Handler 数量达标；所有阶段恰好完成 |
| `ordering` | 先 Post 1，再 Publish 2 | 同一 Serialized channel 的动态观察结果严格为 1、2 |
| `concurrent` | 多工作区并发提交任务 | 每个 partition 内顺序稳定；总数无丢失、无重复 |
| `failure` | 注入可预期 Handler 故障 | PublishResult 可见失败；无关 Handler 按策略继续或跳过 |
| `ownership` | 停止动态订阅 owner | 该 owner 不再接收，静态及其他 owner 不受影响 |
| `cancel-stop` | 并发取消、StopAsync、DisposeAsync | 有界结束、无未观察异常、停止后拒绝新发布 |
| `verify-all` | 顺序执行全部场景 | 输出单行 JSON 总结并以退出码表达成败 |

所有场景必须可重复，不以任意 `Sleep` 或偶发 timing 作为正确性证据。

## 7. 规模门禁

`verify-all` 运行时必须自行验证并报告：

- selected Module 数量不少于 10；
- 生成式业务 Service 类型不少于 30；
- Event Contract 数量不少于 12；
- 静态生成 Handler 数量不少于 20；
- 并发场景至少处理 8 个 workspace、每个 25 个任务；
- 完整执行后所有计数守恒，owner 释放后无额外 delivery；
- Host 停止后 Publish 被拒绝；进程无悬挂后台工作。

## 8. 输出合同

成功时 stdout 最后一行必须为紧凑 JSON，至少包含：

```json
{"status":"passed","modules":10,"services":31,"contracts":12,"handlers":20,"scenarios":6}
```

失败时由共享 `ProcessEntryPoint` 输出完整异常到 stderr，进程返回非零退出码。测试和脚本不得仅搜索普通日志中的 `OK` 字样判绿。

## 9. 构建与发布门禁

最低执行矩阵：

```powershell
dotnet build fixtures/AtomUI.City.CoreEventBus.DogfoodApp/AtomUI.City.CoreEventBus.DogfoodApp.csproj -c Release -p:TreatWarningsAsErrors=true
dotnet run --project fixtures/AtomUI.City.CoreEventBus.DogfoodApp/AtomUI.City.CoreEventBus.DogfoodApp.csproj -c Release -- verify-all
dotnet publish fixtures/AtomUI.City.CoreEventBus.DogfoodApp/AtomUI.City.CoreEventBus.DogfoodApp.csproj -c Release -f net8.0 -r win-x64 -p:AtomUICityCoreEventBusDogfoodPublishAot=true
```

NativeAOT 产物必须真实运行 `verify-all`，并检查退出码和最终 JSON。网络恢复、SDK 或 toolchain 故障应记录为环境失败，不得改写功能断言。

Windows 上的统一执行入口为本目录的 `verify.ps1`。默认执行 Release build、20 轮完整场景以及 net8.0/win-x64 NativeAOT 发布和真实运行；只有排查普通功能时才允许显式传入 `-SkipNativeAot`。

## 10. 完成标准

- 设计中的规模、场景、失败路径和输出合同均有实现。
- Release build 零 warning、零 error。
- `verify-all` 通过且重复执行结果确定。
- Windows NativeAOT 发布及原生进程运行通过。
- 未修改 Core/EventBus 生产代码来迎合 fixture；若 fixture 暴露生产缺陷，必须单独登记并修复后重新验收。
