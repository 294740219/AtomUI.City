# AtomUI.City.EventBus 1.0 RC 验收报告

## 报告状态

- 验收协议：[release-candidate-validation.md](release-candidate-validation.md)
- 候选：Engineering RC1
- 执行状态：已完成
- 开始时间：2026-09-04 11:52:41 +08:00
- 结束时间：2026-09-04 12:11:13 +08:00
- Application Plane MVP 最终结论：**不通过**
- Plugin Plane 最终结论：**EventBus 侧协议仍有阻断缺陷；PluginSystem 集成尚未开始**

## 候选身份与环境

| 项目 | 记录 |
| --- | --- |
| Branch | `develop` |
| HEAD | `a5e28687270e36e8dab60ef383d77daed6b707ab` |
| 工作区 | 非干净；验收开始时 `git status --short` 共 92 项，包含本轮 EventBus 001–009 实现、测试、Generator、fixture、Benchmark 和文档变更，也包含候选范围外变更。 |
| 候选范围文件数 | 207（排除 `bin`、`obj`、临时输出和本报告）。 |
| Scope SHA-256 | `e687e485d027c67464b9b2414af66501919ddb12ce37f929a9c72111e2636eea` |
| OS | Microsoft Windows 10.0.26200.0，x64，RID `win-x64`。 |
| CPU | AMD Ryzen 7 9700X，8 core / 16 logical processors。 |
| global.json | 请求 `10.0.300`，`rollForward=latestFeature`，禁止 prerelease。 |
| 系统 SDK | `10.0.111`、`9.0.317`；不能直接满足仓库 feature band。 |
| 验收 SDK | `output/core-rc/dotnet` 中的隔离 SDK `10.0.300`。 |

未提交候选只能由 `HEAD + Scope SHA-256` 标识，不具备从单一 commit 重建的发布可复现性。输出目录中的 consumer、NuGet cache、NativeAOT 和 Benchmark 产物不属于源码候选。

## Gate 状态

| Gate ID | 状态 | 摘要 |
| --- | --- | --- |
| EVENTBUS-RC-001 | Passed | 候选范围、HEAD、环境和内容指纹已经记录。 |
| EVENTBUS-RC-002 | Failed | Feature 权威清单、详细卡、testing 与 overview 状态互相冲突；008/009 的详细卡仍保留旧状态。 |
| EVENTBUS-RC-003 | Failed | 双 TFM Release 构建零 warning/error、依赖边界测试通过，但 EventBus 无 Public API baseline/analyzer/package validation，两个 XML 各只有 1 个 member。 |
| EVENTBUS-RC-004 | Passed | EventBus 230/230；EventBus Generator 专项 4/4；均零失败、零跳过。 |
| EVENTBUS-RC-005 | Passed | 五组高风险类连续 20/20 轮通过，每轮 89/89，无挂起或竞态失败。 |
| EVENTBUS-RC-006 | Failed | Headless 仅有 EventBusModule + 1 个应用 Module、1 个 contract、1 个 handler，不满足 5+ Module、20+ handler 产品矩阵。 |
| EVENTBUS-RC-007 | Passed | net8/net10 win-x64 NativeAOT 均完成发布和真实运行，输出 `EVENTBUS_AOT_OK`，无 IL2xxx/IL3xxx。 |
| EVENTBUS-RC-008 | Passed | nupkg 结构和依赖组正确；同候选 Core/EventBus 包在隔离 cache 的外部 net8 Host 中完成 Publish/Post/Stop。 |
| EVENTBUS-RC-009 | Observation | ShortRun 实际完成 7/7；无挂起，但覆盖面尚未达到协议完整矩阵。 |
| EVENTBUS-RC-010 | Failed | Markdown diff/link 检查通过，但文档状态不一致且本报告记录了未关闭阻断项。 |

## 第一轮横向审计问题清单

| Issue ID | 优先级 | 问题 | 影响 | 主要证据 | 状态 |
| --- | --- | --- | --- | --- | --- |
| EVENTBUS-AUDIT1-001 | P1 | Generated Shared contract 的对象图验证不是封闭白名单。 | `object`、外部接口或无法证明安全的外部引用类型可进入 Shared payload；插件对象可藏入排队事件并在 lease 释放后继续被 Root EventBus 持有，破坏 ALC 可回收边界。 | `EventMetadataReader.Visit` 对任意 `SpecialType` 直接接受，并在类型来自外部程序集时停止递归；Generator tests 未覆盖开放对象图。 | 后续已修复、已验证 |
| EVENTBUS-AUDIT1-002 | P1 | Plugin contribution drain 没有领域内有界等待、timeout 状态或稳定诊断。 | 忽略取消且永不结束的 handler/operation 会让 lease 唯一终止事务永久停在 Draining；Host 的等待 token 只能取消调用方等待，后台释放仍不会完成，插件 ALC 无法卸载。 | `EventBusContributionLease.TerminateAsync` 无 timeout 地等待 subscription 和 `_operationsDrained`；文档承诺的 `EventPluginDrainTimedOut` 不存在于 `EventDiagnosticIds`。 | 后续已修复、已验证 |
| EVENTBUS-AUDIT1-003 | P1 | Plugin lease 在内部锁内调用总线注册和可重入 diagnostics sink。 | 自定义 sink 重入 Stop/Dispose 时，可在一条 Subscribe 尚未提交到 lease 集合前建立 termination snapshot，形成“Quiescing 后仍提交返回”的半事务；也违反“不在内部锁内执行外部代码”的线程合同。 | `EventBusContributionLease.Subscribe` 持有 `_syncRoot` 调用 `SelectSubscriber(...).Subscribe`；`RequestTermination` 持锁调用 `WriteDiagnostic`。 | 后续已修复、已验证 |
| EVENTBUS-AUDIT1-004 | P1 | Generator 不验证 `IEventHandler<TEvent>` 的 TEvent 是否存在于可达 generated/shared contract catalog。 | 拼写错误、遗漏 `[EventContract]` 或缺失 registrar 在编译期不报错，直到 Host Start 激活 handler 时才因 frozen registry 未知类型失败；不满足文档声明的 Analyzer 门禁。 | `AtomUICityIncrementalGenerator` 只检查重复 ContractId，未建立 handler-event 到本地/引用 contract 的闭包验证；现有 4 个测试无该反例。 | 后续已修复、已验证 |
| EVENTBUS-AUDIT1-005 | P1 | EventBus 1.0 Public API 发布门禁不存在。 | 删除类型、改变签名或遗漏 XML 文档仍可通过当前发布检查，二进制兼容性不可控。 | 项目无 `PublicAPI.Shipped.txt`/`Unshipped.txt`，未启用 PublicApiAnalyzers/PackageValidation；XML 每个 TFM 只有 1 个 member；工程测试只保护 Core。 | 待修复 |
| EVENTBUS-AUDIT1-006 | P1 | 缺少满足产品组合的 Headless CLI dogfood。 | 单元测试和最小 AOT 无法发现 5+ Module、20+ handler、跨 channel/策略/停止组合中的注册遗漏、顺序冲突或资源泄漏。 | `fixtures/AtomUI.City.EventBus.HeadlessApp` 只有 2 个 Module、1 个 handler 和单次 Publish。 | 待设计、待补齐 |
| EVENTBUS-AUDIT1-007 | P2 | Feature 状态与验收矩阵漂移。 | 维护者无法判断哪些 Feature 已验证，可能把 In Design、部分验证和完整验证混为一谈。 | `features.md` 顶部、详细卡、`testing.md`、`overview.md` 对 001、002、008、009 的状态不一致。 | 待修复 |
| EVENTBUS-AUDIT1-008 | P2 | Public metrics snapshot 可直接构造非法值。 | 调用方可得到 default ContractId、未知 ExecutionMode、负容量/计数/时长等语义不可能状态，削弱公共模型合同。 | `EventBusMetricsSnapshot` 与 `EventChannelMetricsSnapshot` 是无校验 positional record；现有测试只验证运行时产生的合法快照。 | 待设计、待修复 |
| EVENTBUS-AUDIT1-009 | P2 | Benchmark 门禁覆盖与执行健壮性不足。 | 默认从不匹配 SDK/工作目录启动时会“实际执行 0 项但退出码仍为 0”，CI 可能假绿；现有场景缺 diagnostics、订阅创建释放、Wait/Reject 压力和 20 handler。 | 两次错误启动均返回 0 且结果 NA；显式把 SDK 10.0.300 放入 PATH 后才实际执行 7 项。 | 待修复 |

### RC1 后续修复记录

- `EVENTBUS-AUDIT1-001`：Shared contract 生成验证已经改为封闭白名单。只接受稳定 scalar、contract-local enum、sealed immutable class、readonly struct 及递归安全的 `Nullable<T>`、`ImmutableArray<T>`、`KeyValuePair<TKey,TValue>`；`object`/`dynamic`、interface、数组、外部类型、可变或可扩展类型均产生 `AUCGEN005`，并阻止对应 registrar contribution 生成。
- generated registrar 使用 `EventContractDescriptor.GeneratedShared<TEvent>` 携带验证证明；手工 `Shared<TEvent>` / `AddEventContract<TEvent>` 不具备该证明，Plugin contribution 在 capability 提交前明确拒绝，防止绕过生成期门禁。
- 2026-09-04 后续验证：Generator EventBus 专项 7/7、Generator 全量 150/150、EventBus 全量（含 net8/net10 win-x64 NativeAOT）231/231 均通过，0 skipped。首次无代理 NativeAOT restore 的两项 `NU1301` 属于受限网络环境；使用既定 `127.0.0.1:7897` 代理后真实发布和执行通过。
- 上述 001 修复记录只关闭 `EVENTBUS-AUDIT1-001`，不反写 RC1 候选身份、原始 Gate 计数或 RC1 最终判定；完整 RC2 仍须按验收协议重新执行。
- `EVENTBUS-AUDIT1-002`：`EventPluginQuotas.DrainTimeout` 现作为 subscription、active operation、plugin Scope 与 private runtime 清理链的单一总 deadline，默认 30 秒。超时后 lease 进入 `Faulted`，以 `EventPluginDrainTimeoutException` 和 `EventBus.EventPluginDrainTimedOut` 报告稳定快照；迟到清理继续受观察，真实结束前 PluginId 保持占用。StopAsync 调用方 cancellation 与领域 deadline 已由测试证明互不混淆。
- 2026-09-04 对 002 的后续验证：EventBusPluginContractTests 12/12；最终 EventBus 全量（包含 net8/net10 win-x64 NativeAOT）234/234 通过，0 skipped。覆盖忽略 cancellation 的 subscription、独立 active publish operation、并发重复终止、timeout 后同名重入拒绝、迟到清理和调用方取消等待。
- 本记录同时关闭 `EVENTBUS-AUDIT1-002`；它仍不反写 RC1 原始 Gate、候选身份或最终判定。
- `EVENTBUS-AUDIT1-004`：Generator 现对每个 generated handler 建立 `TEvent` catalog 闭包。本地事件必须是当前 compilation 的有效 generated contract candidate；引用事件必须具有 `[EventContract]`，且 Event manifest 与 Core service manifest 版本、registrar identity 和 registrar 形状一致。缺失或伪 catalog 产生 `AUCGEN005` 并阻止 registrar 输出。
- Host 的 `EventBusModule.PostConfigureServices` 在 Root Provider 创建前，对实际选中 Module contribution 的 generated handler 与 generated Shared contract 执行第二次闭包验证。只选择 handler owner、遗漏 contract owner 时，`Build()` 立即失败且不会构造或激活任何 handler；运行期 frozen Registry 拒绝仍作为第三层防线保留。
- 2026-09-04 对 004 的后续验证：EventBus Generator 专项 10/10、Generator 全量 153/153、Host/Registration 专项 28/28；EventBus 常规全量 241/241，另行在沙箱外通过既定本地代理完成 net8/net10 win-x64 NativeAOT 2/2，合计 243/243、0 skipped。沙箱内首次 AOT restore 的 2 项 `NU1301` 为本地代理 TLS/凭据隔离，沙箱外同一代码真实发布和执行通过。
- 本记录同时关闭 `EVENTBUS-AUDIT1-004`；它仍不反写 RC1 原始 Gate、候选身份或最终判定。
- `EVENTBUS-AUDIT1-003`：Lease Subscribe 已改为“锁外 contract/capability 验证 → 锁内 pending 配额预留 → 锁外底层注册 → 锁内提交或锁外异步回滚”。Stop 的唯一 Task 先于锁外 Quiescing 诊断发布，并等待 pending registration 完整提交或回滚；Publish/Post 的 Registry 查询与实际 EventBus 调用也已移出 Lease 锁。Controller 提交失败后的 lease Dispose 移至总线锁外，Lease Activated 诊断延后到 PluginId 原子占有之后。
- 2026-09-04 对 003 的后续验证：`EventBusPluginContractTests` 18/18，高风险专项连续 20/20 轮、每轮 18/18；最终 EventBus 全量（包含 net8/net10 win-x64 NativeAOT）240/240 通过，0 skipped。覆盖同线程/跨线程 diagnostics Stop 重入、Subscribe 诊断重入、100 轮 Subscribe/Stop 竞争、pending 外部注册卡住及 timeout 后回滚、Activated 诊断同名抢占。
- 本记录同时关闭 `EVENTBUS-AUDIT1-003`；它仍不反写 RC1 原始 Gate、候选身份或最终判定。

## Feature 证据矩阵

| Feature | 当前审计结论 | 实现/测试证据 | 未关闭项 |
| --- | --- | --- | --- |
| AUC-EVENTBUS-001 | 实现与单测基本闭环，尚不能独立标记产品 Verified。 | Publish/Post、result、correlation、取消与失败测试进入 230 项全量集合。 | 产品 dogfood、Public API 门禁与文档状态。 |
| AUC-EVENTBUS-002 | EventBus 侧生命周期、领域有界 drain 与插件 reentrancy 已在 RC1 后闭环。 | owner、quiesce、drain timeout、pending rollback、并发 Stop/Dispose 与重入高风险集合通过。 | 真实 PluginSystem 集成仍 Pending。 |
| AUC-EVENTBUS-003 | Registry 主实现和失败边界有测试证据。 | 精确 id/type、freeze、DI collected descriptor 测试通过。 | generated handler 到 contract catalog 的编译期闭包校验。 |
| AUC-EVENTBUS-004 | 当前实现与专项测试通过。 | dispatcher、错误策略、timeout、lingering handler 测试进入 20 轮集合。 | 仍受产品 dogfood 和发布门禁阻断。 |
| AUC-EVENTBUS-005 | 诊断主路径、sink 隔离和插件重入边界已在 RC1 后闭环。 | stable code/context、metrics、payload projection、`EventPluginDrainTimedOut`、同/跨线程 sink 重入测试通过。 | 仍受产品 dogfood 与 Public API 门禁阻断。 |
| AUC-EVENTBUS-006 | Host 集成专项测试通过。 | Start/Stop/Stop-before-Start、Scope、DI 与回滚测试进入 20 轮集合。 | 真实多模块产品组合未建立。 |
| AUC-EVENTBUS-007 | channel runtime 专项和压力重复通过。 | bounded capacity、五类背压、三种执行模式、partition/runtime limit 测试通过。 | 更完整 Benchmark 与 dogfood。 |
| AUC-EVENTBUS-008 | NativeAOT/生成闭环通过；对象图缺口已在 RC1 后关闭。 | RC1 Generator 4/4；后续 Generator EventBus 专项 7/7；net8/net10 NativeAOT 真实运行。 | handler-contract 编译期闭包。 |
| AUC-EVENTBUS-009 | RC1 后已关闭领域 drain timeout 与 reentrancy 原子性，仍不能恢复完整 Verified。 | capability、private plane、三阶段 admission、有界 lease drain、迟到清理、重入和简单 collectible ALC 测试通过。 | 真实 PluginSystem 集成仍 Pending。 |

## 门禁执行证据

### 构建、测试与依赖

- EventBus Release 同时产出 net10.0/net8.0 DLL，0 warning、0 error。
- EventBus 全量测试：230/230，通过，0 skipped。
- EventBus Generator 专项：4/4，通过，0 skipped。
- Build dependency/package 测试筛选：15/15，通过；审计确认 package/Public API 断言只覆盖 Core，不能作为 EventBus API gate 证据。
- 高风险集合每轮 89/89，连续 20/20 轮成功，单轮 wall-clock 约 1.55–1.66 秒。

首次 restore 因沙箱网络权限失败；使用用户提供的 `127.0.0.1:7897` 代理后确定性成功。该首次错误属于环境准备，不归因于候选代码。

### Public API 与包

`AtomUI.City.EventBus.1.0.0.nupkg` 包含：

```text
lib/net10.0/AtomUI.City.EventBus.dll|pdb|xml
lib/net8.0/AtomUI.City.EventBus.dll|pdb|xml
LICENSE
README.nuget.md
RELEASE_NOTES.md
```

nuspec 的两个 TFM 均只依赖 `AtomUI.City.Core 1.0.0`，repository URL/branch/commit 正确。两个 XML 文件各只有 1 个 member；EventBus project 没有启用 Core 已具备的 Public API analyzer 和 package validation。

外部 net8 消费者使用独立 NuGet cache 和 source mapping，强制 `AtomUI.City.*` 从本地 RC feed 还原，最终输出 `EVENTBUS_PACKAGE_CONSUMER_OK`。复测同时证明，相同 1.0.0 的旧 Core 全局缓存会造成候选二进制混用；正式发布必须使用唯一包版本和同一 commit 产物。

### NativeAOT

| TFM/RID | Publish | Native run | 结果 |
| --- | --- | --- | --- |
| net8.0/win-x64 | Passed | Passed | `EVENTBUS_AOT_OK` |
| net10.0/win-x64 | Passed | Passed | `EVENTBUS_AOT_OK` |

输出未出现 IL2xxx、IL3xxx 或 `will always throw`。该证据覆盖最小 generated contract/handler 闭环，不等价于 EVENTBUS-RC-006 产品组合。

### 性能观察

BenchmarkDotNet 0.15.8、SDK 10.0.300、.NET 10.0.8、ShortRun（1 launch、3 warmup、3 iteration）：

| 场景 | Mean | Allocated |
| --- | ---: | ---: |
| Publish，0 subscriber | 1.428 us | 1.66 KB |
| Publish，1 subscriber | 4.321 us | 8.32 KB |
| Publish，16 subscribers | 39.018 us | 102.08 KB |
| Publish，已有 1 channel runtime | 1.491 us | 1.66 KB |
| Publish，已有 64 channel runtimes | 1.367 us | 1.66 KB |
| Publish，已有 256 channel runtimes | 1.438 us | 1.66 KB |
| runtime limit rejection | 353.1 ns | 2.53 KB |

未发现 channel cardinality 随 1/64/256 显著退化。订阅 fan-out 的耗时和分配近似随数量增长；首份数据没有历史基线，不作回归判定。当前 Benchmark 缺少协议要求的 diagnostics on/off、20 handler、订阅创建/释放、Wait/Reject 压力等场景。

两次环境错误启动都由 BenchmarkDotNet 返回退出码 0，但报告为 0 executed/NA；验收脚本未来必须解析报告或强制检查结果数量，不能只看进程退出码。

### 测试基础设施观察：TEST-INFRA-001

该问题属于跨模块测试进程边界，不计入 EventBus 的 6 个 P1、3 个 P2 产品审计统计。2026-09-03 的 Windows `.NET Runtime` 日志保留了 8 次 `dotnet.exe` 未处理托管异常：6 次为 TodoCli DI 无法解析 `System.String[]`，2 次为无法解析 `TodoCli.ITodoFormatter`；另观察到 EventBus 外部消费者的 `0xe0434352` 系统错误框。已知具体配置缺失随后均已补齐，但 fixture/消费者异常逃出入口并由 WER 显示，证明进程测试缺少统一顶层异常边界和非交互式失败策略。

RC2 的 Headless、NativeAOT 和外部消费者必须使用 `stderr + 非零退出码 + 有界等待` 报告故障，并在 Windows 下仅为测试子进程抑制错误对话框。任何需要人工点击的系统弹窗使对应 Gate 失败；不得修改机器级或用户级 WER 配置。

后续工程修复已建立共享 fixture 入口边界和测试进程运行器，并增加 Core Headless、Core MVP CLI、EventBus JIT/双 TFM NativeAOT 的故障注入断言。修复后的故障进程均以非零退出码和完整 `stderr` 结束；验证期间 Windows Application 日志新增 `.NET Runtime` 1026 未处理异常为 0。该证据关闭 `TEST-INFRA-001`，但不反写 RC1 的候选身份或最终判定。

## 已知范围限制

- Presentation 真实 UI dispatcher 集成不属于本次范围；fake dispatcher 合同测试已执行。
- PluginSystem 尚未施工 EventBus 009 的 manifest、DI Scope、capability 和真实动态 ALC 集成，因此 Plugin Plane 不能完整 Verified。
- 工作区未提交，当前 Engineering RC 不具备正式发布的单 commit 可复现性。

## 最终判定

Engineering RC1 **不通过**。EventBus 的主体设计和实现已经有较强基础：双 TFM 构建、230 项全量测试、20 轮并发重复、双 NativeAOT、nupkg 外部消费及初始 Benchmark 均提供了有效证据；但 6 个 P1 与 3 个 P2 尚未关闭，尤其是 Shared 对象图/插件卸载边界、插件 drain 有界性、lease 原子性、Public API gate 和产品级 dogfood，均不能以文档修正或现有单测全绿替代。

建议按问题清单顺序先冻结并修复 `AUDIT1-001` 至 `004` 的运行时/Generator 合同，再补 `005/006` 发布与产品门禁，最后处理 P2 并形成 RC2。RC2 必须重新计算指纹并至少重跑 EVENTBUS-RC-003、004、005、006、007 和 010。
