# AtomUI.City.Core 1.0 Release Candidate Validation

## 目的

本文件定义 AtomUI.City.Core 作为 City 最小 MVP 1.0 产品的发布候选（RC）验收协议。验收回答的是“当前候选是否完整实现已经冻结的 Core 1.0 合同”，不是继续扩展 Feature 或评价 City 其他模块的完成度。

验收必须遵循“先冻结本协议，再执行，再记录证据”的顺序。执行期间不得为了让结果通过而修改命令、阈值或判定规则；发现缺陷后，本候选判为失败，修复形成新候选，再按变更控制规则复验。

## 权威输入与范围

- [features.md](features.md) 是 Feature 数量、编号和状态的权威清单；当前基线为 `AUC-CORE-001` 至 `AUC-CORE-008`，即 `8/8 Verified`。
- [api-contracts.md](api-contracts.md)、[architecture.md](architecture.md)、[compatibility.md](compatibility.md)、[threading.md](threading.md) 和 [testing.md](testing.md) 定义行为、线程、兼容性与测试合同。
- 产品范围包括 `src/AtomUI.City.Core`、Core 所需的 `src/AtomUI.City.Generators` 生成链、Core 测试、Core Headless/MVP fixtures、Core 工程门禁和 `docs/modules/core`。
- Generator 验收只覆盖 Modularity 与 Dependency Injection 两条 Core 生产链，不据此评价 Presentation、Routing、Localization 或 Plugin 生成能力。
- Build tests 只作为 Core 的 Public API、依赖边界和包结构证据，不据此评价 City 的整体工程状态。

Presentation、CLI 产品功能、Templates、Testing、其他业务模块、插件独立 ServiceProvider、桌面 `IApplicationLifetime` 和未分配 Core Feature ID 的路线图内容不属于本次验收。

## 候选冻结与环境记录

执行前必须记录：

- 当前分支、HEAD commit、工作区是否干净；
- 若工作区不干净，记录全部变更路径，并对候选范围内文件计算稳定 SHA-256 内容指纹；
- `global.json`、实际 .NET SDK/runtime、操作系统、架构和 RID；
- 验收开始时间与结束时间。

干净且已提交的候选以 commit 标识。未提交候选只能作为工程 RC 验收对象，以 `HEAD + scope fingerprint` 标识；即使功能门禁全部通过，也必须在结论中披露其发布可复现性限制。

验收报告文件不进入候选产品内容指纹。报告只追加事实，不得反向改变本协议。

## 结论规则

单项状态只有 `Passed`、`Failed`、`Not Run` 和 `Observation`：

- 任一现行 Core 合同违反、编译/测试失败、非预期 warning、死锁、超时、竞态失败、NativeAOT 失败、Public API 漂移或包不可消费，整体为 `不通过`。
- 仅属于未承诺路线图的能力不得制造失败，也不得标记为已经实现。
- 性能耗时在首份稳定基线形成前为 `Observation`；内存无界增长、进程挂起、复杂度数量级异常或测量过程失败仍是阻断项。
- `Not Run` 的阻断门禁使整体为 `不通过`。
- 所有阻断门禁通过，但候选存在已明确且不违反 Core 1.0 合同的发布操作限制时，可以判为 `有条件通过`。
- 所有阻断门禁通过、候选可复现且没有未接受的限制时，判为 `通过`。

测试失败不得简单归类为偶发。必须保留首次失败证据；重跑通过只能说明存在不稳定性，除非证明失败来自验收对象之外的确定性环境故障。

## 门禁总表

| Gate ID | 门禁 | 类型 | 通过标准 |
| --- | --- | --- | --- |
| CORE-RC-001 | 候选身份与环境 | Blocking | 候选范围、版本、变更和环境完整记录，内容指纹可重复计算。 |
| CORE-RC-002 | Feature 与合同追踪 | Blocking | 8 个 Feature 均能追踪到实现、测试及失败路径，Core 文档内部数量和状态一致。 |
| CORE-RC-003 | Release 构建、Public API 与依赖边界 | Blocking | net10/net8 Release 零错误、零 warning；Public API、XML 文档、package validation 和 Core 依赖边界通过。 |
| CORE-RC-004 | Core 与 Generator 自动化测试 | Blocking | Core 全量测试和 Core 相关 Generator 测试全部通过。 |
| CORE-RC-005 | 生命周期与并发重复验证 | Blocking | 指定高风险测试连续 20 轮通过，无挂起、竞态失败或异常进程。 |
| CORE-RC-006 | Headless 与 MVP JIT dogfood | Blocking | 真实进程矩阵通过；MVP 返回 6 Module、27 Service、120 排列、32 组合和 64 concurrent scopes。 |
| CORE-RC-007 | net10/net8 NativeAOT | Blocking | 两个 TFM 的 win-x64 restore/publish/run 全部通过，无 IL2xxx、IL3xxx 或 `will always throw`。 |
| CORE-RC-008 | NuGet 产物与外部消费 | Blocking | Core nupkg 结构正确，外部临时项目仅从本地包源还原、编译并运行。 |
| CORE-RC-009 | 性能观察基线 | Observation | 环境、样本和 JIT MVP 进程数据已记录；不得出现挂起或数量级离群。 |
| CORE-RC-010 | 文档与证据闭环 | Blocking | 报告包含每项命令、退出码、结果、限制和最终判定；Core 文档链接及格式检查通过。 |

## CORE-RC-001 候选身份与环境

候选范围指纹覆盖下列现存文件：

```text
src/AtomUI.City.Core/**
src/AtomUI.City.Generators/**
tests/AtomUI.City.Core.Tests/**
tests/AtomUI.City.Generators.Tests/**
tests/AtomUI.City.Build.Tests/**
fixtures/AtomUI.City.Core.HeadlessApp/**
fixtures/AtomUI.City.Core.CyclicModules/**
fixtures/AtomUI.City.Core.Mvp/**
engineering/check-core-mvp-aot.ps1
engineering/check-public-api.sh
engineering/check-dependency-boundaries.sh
docs/modules/core/**
Directory.Build.props
Directory.Build.targets
Directory.Packages.props
global.json
build/**
```

文件按仓库相对路径序排列；每个文件先计算 SHA-256，再对 UTF-8 编码的 `relative-path<TAB>file-hash<LF>` 清单计算总 SHA-256。验收报告自身排除在外。

## CORE-RC-002 Feature 与合同追踪

逐行检查 [features.md](features.md) 和 [testing.md](testing.md)：

- Feature ID 集合必须完全相同，恰好为连续的 001 至 008；
- 每项必须有 public/internal contract、失败行为和至少一个具体测试证据；
- `Verified` 不得依赖未执行或不存在的测试；
- `overview.md` 的成熟度描述必须区分当前 8 个 Feature 和未分配 ID 的路线图。

报告中必须给出 8 行证据矩阵，不能只写“文档检查通过”。

## CORE-RC-003 Release 构建、Public API 与依赖边界

从仓库根目录执行：

```powershell
dotnet restore src/AtomUI.City.Core/AtomUI.City.Core.csproj -p:Configuration=Release
dotnet build src/AtomUI.City.Core/AtomUI.City.Core.csproj -c Release --no-restore -p:TreatWarningsAsErrors=true
bash engineering/check-public-api.sh
dotnet test tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj -c Release --filter "FullyQualifiedName~ProjectDependencyBoundaryTests|FullyQualifiedName~PackagingReleaseGateTests|FullyQualifiedName~CoreFeatureStatusesStaySynchronized" -p:TreatWarningsAsErrors=true
```

必须明确记录两个 TFM 的 Core DLL/XML 产物。`check-public-api.sh` 必须确认 baseline 非空、XML 文档有成员、每个 TFM 的 SourceLink JSON 使用标准 GitHub raw URL、nupkg repository commit 等于 HEAD、PublicApiAnalyzers 生效且 Core 包通过 SDK package validation。Build tests 中与非 Core 包直接相关的失败需在报告中隔离说明，不能误记为 Core 通过。

## CORE-RC-004 Core 与 Generator 自动化测试

执行 Release 全量 Core 测试：

```powershell
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj -c Release -p:TreatWarningsAsErrors=true
```

执行 Core 使用的 Generator 测试类：

```powershell
dotnet test tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj -c Release --filter "FullyQualifiedName~AtomUICityIncrementalGeneratorDependencyInjectionTests|FullyQualifiedName~AtomUICityIncrementalGeneratorModularityTests|FullyQualifiedName~ServiceRegistrationMetadataReaderTests|FullyQualifiedName~ServiceRegistrationManifestBuilderTests|FullyQualifiedName~ModuleDependencyGraphBuilderTests|FullyQualifiedName~ModuleMetadataReaderTests|FullyQualifiedName~ModuleRegistrarSourceBuilderTests" -p:TreatWarningsAsErrors=true
```

报告必须记录发现、通过、失败和跳过数量。跳过项必须逐项说明，不能把“零失败”自动等同于完整通过。

## CORE-RC-005 生命周期与并发重复验证

下列高风险集合连续执行 20 轮，每轮设置 5 分钟进程上限：

```powershell
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ApplicationHostIndustrialLifecycleTests|FullyQualifiedName~ApplicationHostRuntimeTests|FullyQualifiedName~LifecycleMiddlewarePipelineTests|FullyQualifiedName~LifecycleScopeTreeTests|FullyQualifiedName~ModuleRegistryConcurrencyTests|FullyQualifiedName~HostDiagnosticsTests"
```

每轮必须退出码为零。任何一轮超时或失败都保留为 Gate failure，不因后续重跑成功而消失。

## CORE-RC-006 Headless 与 MVP JIT dogfood

Core 全量测试已经通过真实 `CoreHeadlessProcessTests` 和 `CoreMvpCliProcessTests` 进程边界。除此之外，直接运行产品入口：

```powershell
dotnet output/bin/Release/AtomUI.City.Core.MvpCli/net10.0/AtomUI.City.Core.MvpCli.dll verify --scenario all
```

最后一行 JSON 必须满足：

```text
success=true
selectedModuleCount=6
selectedServiceCount=27
permutationCount=120
combinationCount=32
concurrentScopeCount=64
failures=[]
```

## CORE-RC-007 net10/net8 NativeAOT

使用仓库脚本：

```powershell
./engineering/check-core-mvp-aot.ps1
```

网络恢复需要代理时，只对该进程设置：

```powershell
$env:HTTP_PROXY="http://127.0.0.1:7897"
$env:HTTPS_PROXY="http://127.0.0.1:7897"
./engineering/check-core-mvp-aot.ps1
```

脚本必须分别验证 `net10.0/win-x64` 与 `net8.0/win-x64`，并解析产品 JSON，而不只是检查可执行文件存在。

## CORE-RC-008 NuGet 产物与外部消费

先使用 CORE-RC-003 生成的 Core 包。检查 nupkg 至少包含：

- `lib/net10.0/AtomUI.City.Core.dll` 与 XML；
- `lib/net8.0/AtomUI.City.Core.dll` 与 XML；
- LICENSE、README 和 release notes；
- 合理的依赖组，且不包含测试、CLI、Avalonia、AtomUI 或 Roslyn runtime 依赖。

随后在 `output/core-rc/consumer` 创建临时 `net8.0` Console 项目，只配置本地 package source，安装当前版本 `AtomUI.City.Core`，编译并运行最小 Host Build/Start/Stop/Dispose 程序。消费项目属于验收产物，不进入仓库源码。

## CORE-RC-009 性能观察基线

首份 RC 只建立本机观察值，不宣称跨机器 SLA。对 Release JIT MVP `verify --scenario all` 运行 10 个独立进程，记录每次 wall-clock、exit code 与 peak working set，并报告中位数、最小值、最大值。第一次进程作为冷观察保留，不从结果中删除。

这组数据覆盖完整 MVP 组合矩阵的产品级成本，但不能替代未来独立 Benchmark 对 Build/Start/Stop、模块图、Diagnostics、Scope 和 DI 的微观测量。没有历史基线时不以毫秒数阻断；任一进程超时、失败，或单次值超过其余样本中位数 5 倍且复测可重现，视为阻断异常。

## CORE-RC-010 文档与证据闭环

执行 Core 文档的链接、Feature ID 和 Markdown 格式检查，至少包括：

```powershell
git diff --check -- docs/modules/core
```

验收报告必须位于 `docs/modules/core/release-candidate-report.md`，包含：候选身份、环境、每个 Gate 的状态、原始命令摘要、数量化结果、8 Feature 证据矩阵、性能观察、已知限制和最终结论。

## 变更控制与复验

验收开始后若修改任何候选范围文件：

1. 当前候选立即停止累积“通过”结论；
2. 记录失败和修复对应关系；
3. 重新计算 scope fingerprint，形成下一个候选；
4. 重跑全部受影响 Gate；
5. 最后重跑 CORE-RC-003、CORE-RC-004、CORE-RC-006、CORE-RC-007 和 CORE-RC-010。

不得在同一候选身份下混用修改前后的测试证据。
