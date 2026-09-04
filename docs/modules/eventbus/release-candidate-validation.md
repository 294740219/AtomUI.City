# AtomUI.City.EventBus 1.0 Release Candidate Validation

## 目的

本文件定义 AtomUI.City.EventBus 作为 City 第二个可独立消费的 MVP 模块的发布候选（RC）验收协议。验收回答“当前候选是否完整实现已经冻结的 EventBus 1.0 Application Plane 合同”，并单独报告 Plugin Plane 的 EventBus 侧协议状态；它不评价 PluginSystem 或 City 整体完成度。

验收必须遵循“先冻结本协议，再执行，再记录证据”的顺序。执行期间不得为了得到通过结论而降低命令、阈值或判定规则。若发现候选缺陷，本候选判为失败；修复后形成新候选并按变更控制复验。

## 权威输入与范围

- [features.md](features.md) 是 Feature 编号、范围和状态的权威清单，基线为 `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-009`。
- [api-contracts.md](api-contracts.md)、[architecture.md](architecture.md)、[compatibility.md](compatibility.md)、[threading.md](threading.md)、[diagnostics.md](diagnostics.md) 和 [testing.md](testing.md) 定义行为、线程、诊断、兼容性与测试合同。
- Application Plane MVP 由 `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 组成；这八项必须全部 Verified 才能通过本次产品验收。
- `AUC-EVENTBUS-009` 当前只验收 EventBus 所有的 contribution request/controller/lease、Shared/Private plane、capability、配额、drain 和引用释放协议。真实 manifest 映射、插件 DI Scope、动态装载和最终 ALC 卸载由 PluginSystem 集成验收，不得在缺少该证据时标记完整 Verified。
- 产品范围包括 `src/AtomUI.City.EventBus`、EventBus 使用的 `src/AtomUI.City.Generators` 生产生成链、EventBus 测试、Headless fixture、Benchmark、相关工程门禁及 `docs/modules/eventbus`。
- Core 只作为已经验收的底座和集成依赖；本轮若发现由 EventBus 改动引起的 Core 回归，则属于阻断项，但不据此重新评价整个 Core 模块。

Presentation 真实 UI dispatcher、PluginSystem 真实动态插件、跨进程 transport、事件持久化和分布式消息能力不属于本次 Application Plane MVP 范围。

## 候选冻结与环境记录

执行前必须记录：

- branch、HEAD、工作区状态以及候选范围内的全部变更；
- 工作区非干净时，按仓库相对路径排序计算候选范围文件的 SHA-256 内容指纹；
- `global.json`、实际 SDK/runtime、操作系统、架构和 RID；
- 验收起止时间。

干净候选以 commit 标识。未提交候选只能作为 Engineering RC，以 `HEAD + scope fingerprint` 标识；功能通过不等于具备从单一 commit 重建的发布可复现性。

验收报告不进入候选内容指纹。报告只能记录事实，不能反向更改本协议。

## 结论规则

单项状态只有 `Passed`、`Failed`、`Not Run` 和 `Observation`：

- 任一冻结合同违反、编译或测试失败、非预期 warning、死锁、超时、竞态失败、NativeAOT/trimming 失败、Public API 漂移、包不可消费或文档状态失真，Application Plane MVP 整体为 `不通过`。
- Headless、NativeAOT、Benchmark 或外部消费者出现需要人工点击的系统错误框，或者预期失败没有保留 `stderr`、非零退出码和有界结束证据，相应 Gate 为 `Failed`。验收不得通过全局关闭 WER 掩盖该问题。
- `AUC-EVENTBUS-009` 的 PluginSystem 集成缺口按既定范围单独报告，不自动使 Application Plane MVP 失败；EventBus 侧协议自身失败仍是阻断项。
- 性能绝对耗时在首份稳定基线形成前是 `Observation`；无界增长、挂起、数量级退化或测量过程失败仍是阻断项。
- 任一阻断门禁为 `Not Run` 时，整体为 `不通过`。
- 全部阻断门禁通过但候选未提交或存在已接受的发布操作限制时，只能判为 `有条件通过`。
- 全部阻断门禁通过、候选可复现且没有未接受限制时，判为 `通过`。

首次失败必须保留；后续重跑通过不能抹去潜在不稳定性，除非能证明首次失败来自验收对象之外的确定性环境故障。

## 门禁总表

| Gate ID | 门禁 | 类型 | 通过标准 |
| --- | --- | --- | --- |
| EVENTBUS-RC-001 | 候选身份与环境 | Blocking | 范围、版本、变更、环境和稳定内容指纹完整记录。 |
| EVENTBUS-RC-002 | Feature、合同与实现追踪 | Blocking | 001–008 均可追踪到实现、正反测试和诊断/失败行为；009 的两阶段状态在全部文档中一致。 |
| EVENTBUS-RC-003 | Release 构建、Public API 与依赖边界 | Blocking | net8/net10 Release 零错误零 warning；只有允许的生产依赖；Public API、XML、SourceLink 和 package validation 有效。 |
| EVENTBUS-RC-004 | EventBus 与 Generator 自动化测试 | Blocking | EventBus 全量测试及 EventBus generator 专项测试全部通过，无未解释 skip。 |
| EVENTBUS-RC-005 | 生命周期、并发与压力重复验证 | Blocking | 指定高风险测试连续 20 轮通过，无超时、竞态、丢失、重复 delivery 或异常进程。 |
| EVENTBUS-RC-006 | Headless CLI 产品组合 | Blocking | 真实 Host 中至少 5 个 Module、20 个 handler、多 channel/partition、Publish/Post、失败策略、Stop/Dispose 和并发组合全部通过。 |
| EVENTBUS-RC-007 | net8/net10 trimming 与 NativeAOT | Blocking | 两个 TFM 的 win-x64 原生发布与真实运行成功，无 IL2xxx、IL3xxx 或反射缺口。 |
| EVENTBUS-RC-008 | NuGet 产物与外部消费 | Blocking | 包结构正确；隔离消费者只从本地包源还原并完成 Host/EventBus 发布、订阅和释放。 |
| EVENTBUS-RC-009 | 性能与资源观察基线 | Observation | Benchmark 可重复执行并覆盖发布、订阅、channel 扩张、背压和 diagnostics；无挂起或资源无界增长。 |
| EVENTBUS-RC-010 | 文档与证据闭环 | Blocking | 报告包含每项命令、数量结果、限制、Feature 矩阵和最终判定；EventBus 文档链接、状态与格式一致。 |

## EVENTBUS-RC-001 候选身份与环境

候选指纹覆盖：

```text
src/AtomUI.City.EventBus/**
src/AtomUI.City.Generators/**
tests/AtomUI.City.EventBus.Tests/**
tests/AtomUI.City.Generators.Tests/**
tests/AtomUI.City.Build.Tests/**
fixtures/AtomUI.City.EventBus.HeadlessApp/**
benchmarks/AtomUI.City.EventBus.Benchmarks/**
docs/modules/eventbus/**
Directory.Build.props
Directory.Build.targets
Directory.Packages.props
global.json
build/**
```

排除 `bin`、`obj`、临时 NativeAOT 输出和验收报告。每个文件计算 SHA-256，再对 UTF-8 编码的 `relative-path<TAB>file-hash<LF>` 清单计算总 SHA-256。

## EVENTBUS-RC-002 Feature、合同与实现追踪

报告必须为 001–009 各给出一行，记录 Feature 状态、关键 API/internal control plane、实现入口、正向测试、失败路径测试和未完成依赖。必须核对：

- `features.md`、`testing.md`、`overview.md` 的状态口径一致；
- 001–008 的 `Verified` 均来自实际执行证据，不由源码或测试文件“存在”推断；
- 009 只能标为 EventBus Contract Verified / PluginSystem Integration Pending，直到真实 PluginSystem 测试完成；
- 文档中承诺但 public API 或实现不存在，以及实现已经发布但未进入兼容性合同，均为失败。

## EVENTBUS-RC-003 Release 构建、Public API 与依赖边界

至少执行：

```powershell
dotnet restore src/AtomUI.City.EventBus/AtomUI.City.EventBus.csproj -p:Configuration=Release
dotnet build src/AtomUI.City.EventBus/AtomUI.City.EventBus.csproj -c Release --no-restore -p:TreatWarningsAsErrors=true
dotnet test tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj -c Release --filter "FullyQualifiedName~ProjectDependencyBoundaryTests|FullyQualifiedName~PackagingReleaseGateTests" -p:TreatWarningsAsErrors=true
```

必须确认 net8/net10 DLL 与 XML、EventBus Public API baseline、SourceLink、nupkg repository metadata、package validation 和依赖方向。若现有工程脚本只验证 Core，则不能把脚本成功误记为 EventBus 已受保护。

## EVENTBUS-RC-004 EventBus 与 Generator 自动化测试

```powershell
dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj -c Release -p:TreatWarningsAsErrors=true
dotnet test tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj -c Release --filter "FullyQualifiedName~EventBusGeneratorTests" -p:TreatWarningsAsErrors=true
```

报告记录发现、通过、失败和跳过数量。NativeAOT process tests 计入本 Gate 的功能证据，但不能替代 EVENTBUS-RC-007 的独立双 TFM 复验。

## EVENTBUS-RC-005 生命周期、并发与压力重复验证

以下高风险测试连续执行 20 轮，每轮进程上限 5 分钟：

```powershell
dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EventChannelRuntimeTests|FullyQualifiedName~EventDispatchAndFailurePolicyTests|FullyQualifiedName~EventSubscriptionTests|FullyQualifiedName~EventBusHostIntegrationTests|FullyQualifiedName~EventBusPluginContractTests"
```

必须使用可控 barrier 的测试覆盖 Publish/Post admission、owner cancellation、subscription drain、Host stop、plugin contribution drain、drop/coalesce、partition 与 dispatcher 竞争。任一轮失败或超时都使 Gate 失败。

## EVENTBUS-RC-006 Headless CLI 产品组合

fixture 必须通过 City Host 启动 EventBus，不得在测试入口绕过 Module/DI/lifecycle control plane。最小矩阵必须包含：

- 5 个以上 application Module；
- 20 个以上静态/generated 或动态 handler；
- 默认与命名 channel、Serialized/Partitioned/Concurrent；
- PublishAsync/PostAsync 共享 admission 与顺序证据；
- ContinueAndReport、StopPublication、FailPublisher、DisableSubscription；
- owner cancellation、Host Stop-before-Start、正常 Stop/Dispose 和并发停止；
- diagnostics/metrics 快照及零悬挂后台操作。

产品进程必须输出机器可解析的最终结果和非零失败退出码。只输出一条 smoke 消息不能通过本 Gate。

## EVENTBUS-RC-007 net8/net10 trimming 与 NativeAOT

对 Headless 产品入口分别执行 `net8.0/win-x64` 和 `net10.0/win-x64` restore、NativeAOT publish 及原生进程运行。验收必须检查退出码与产品结果，并扫描 IL2xxx、IL3xxx、`RequiresUnreferencedCode` 和 `will always throw`；不能只确认 exe 文件存在。

网络恢复需要代理时，只为验收进程设置：

```powershell
$env:HTTP_PROXY="http://127.0.0.1:7897"
$env:HTTPS_PROXY="http://127.0.0.1:7897"
```

## EVENTBUS-RC-008 NuGet 产物与外部消费

包至少包含 net8/net10 的 EventBus DLL、PDB、XML、LICENSE、README 和 release notes，且不得携带测试、fixture、Benchmark、Roslyn runtime 或无关 UI 依赖。

在 `output/eventbus-rc/consumer` 创建隔离 net8 Console，只配置本地 package source，安装当前版本 Core 与 EventBus 包，编译并真实运行最小 Host：注册 contract、启动 EventBus、绑定 owner 订阅、Publish/Post、停止并释放。消费项目属于验收产物，不进入源码指纹。

## EVENTBUS-RC-009 性能与资源观察基线

Release Benchmark 至少覆盖：

- 无订阅、单订阅、20 个订阅的 Publish；
- 首次与重复 Publish；
- 1、64、256 个 channel runtime；
- Wait/Reject 和全局 runtime 上限；
- diagnostics 开/关；
- 大量订阅建立/释放及并发写入。

首份基线不承诺跨机器绝对毫秒数。报告记录 SDK、CPU、样本、吞吐、延迟、分配量和异常；运行失败、挂起或可复现的数量级离群是阻断问题。

## EVENTBUS-RC-010 文档与证据闭环

至少执行：

```powershell
git diff --check -- docs/modules/eventbus
```

验收报告位于 `docs/modules/eventbus/release-candidate-report.md`，必须包含候选身份、环境、Gate 状态、原始命令摘要、测试数量、Feature 证据矩阵、性能观察、已知限制及 Application Plane/Plugin Plane 两个明确结论。

## 变更控制与复验

验收开始后若修改候选范围文件：

1. 当前候选停止累积“通过”结论；
2. 记录缺陷与修复关系；
3. 重新计算 scope fingerprint，形成下一候选；
4. 重跑全部受影响门禁；
5. 最后至少重跑 EVENTBUS-RC-003、004、006、007 和 010。

不得在同一候选身份下混用修改前后的测试证据。
