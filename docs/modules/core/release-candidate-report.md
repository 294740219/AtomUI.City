# AtomUI.City.Core 1.0 RC 验收报告

## 报告状态

- 验收协议：[release-candidate-validation.md](release-candidate-validation.md)
- 候选：Engineering RC1
- 执行状态：已完成
- 开始时间：2026-09-03 11:58:23 +08:00
- 结束时间：2026-09-03 12:29:49 +08:00
- 最终结论：**不通过**

## 候选身份与环境

| 项目 | 记录 |
| --- | --- |
| Branch | `develop` |
| HEAD | `a7a092d59198c12dd70fb1b7c3baec21503a9bee` |
| 工作区 | 非干净；验收开始时 `git status --short` 共 137 项，包含三轮 Core 审计的未提交实现、测试、fixture、工程及文档变更，也包含候选范围外变更。 |
| 候选范围文件数 | 268（排除 `bin`、`obj` 与本报告） |
| Scope SHA-256 | `178c1202bcf54fdd46b62d842587abdeb5ea4ce0f208ed621dd3311c8de3b769` |
| OS | Microsoft Windows 10.0.26200, x64 |
| CPU | AMD64 Family 26 Model 68 Stepping 0, AuthenticAMD |
| RID | `win-x64` |
| global.json | 请求 `10.0.300`，`rollForward=latestFeature`，禁止 prerelease。 |
| 初始系统 SDK | `10.0.111`、`9.0.317`；均不能满足 `10.0.300` feature band，首次 `dotnet --version` 按预期拒绝解析。 |
| 验收 SDK | `output/core-rc/dotnet` 隔离安装的 `10.0.300`。 |

未提交候选以 `HEAD + Scope SHA-256` 标识。由于不是干净 commit，本报告最终即使功能门禁全部通过，也必须披露源码发布可复现性限制。

## Gate 状态

| Gate ID | 状态 | 摘要 |
| --- | --- | --- |
| CORE-RC-001 | Passed | 候选范围、环境及内容指纹已记录；锁定 SDK 10.0.300 已隔离准备。 |
| CORE-RC-002 | Failed | 索引和测试矩阵均为 8/8 Verified，但 AUC-CORE-005 详细卡仍标记 `In Progress`。 |
| CORE-RC-003 | Failed | 双 TFM 构建、Public API、package validation 和 15 项 Build tests 通过，但 Release 产生 SourceLink warning，不满足零 warning。 |
| CORE-RC-004 | Failed | Core 253/254，通过的 Generator 专项测试为 42/42；配置冻结测试确定性失败。 |
| CORE-RC-005 | Passed | 指定生命周期与并发高风险集合连续 20/20 轮通过。 |
| CORE-RC-006 | Passed | Headless 测试无单独失败；MVP JIT 产品进程返回完整 6/27/120/32/64 矩阵。 |
| CORE-RC-007 | Passed | net10/net8 win-x64 NativeAOT restore、publish 与原生运行全部通过，无 AOT/trimming 风险提示。 |
| CORE-RC-008 | Passed | nupkg 内容正确；独立 net8 包消费者零 warning 构建并完成 Host 生命周期。 |
| CORE-RC-009 | Observation | 10/10 JIT 产品进程成功，wall-clock 与工作集观察无数量级离群。 |
| CORE-RC-010 | Passed | Core Markdown 相对链接、代码围栏和 `git diff --check` 均通过，报告证据完整。 |

## 环境预检证据

协议自检确认 10 个 Gate ID 连续存在，引用的工程、脚本和测试入口均存在，`git diff --check -- docs/modules/core` 退出码为 0。

首次执行 `dotnet --version` 时，SDK resolver 报告：请求版本 `10.0.300`，本机仅安装 `10.0.111` 和 `9.0.317`。依据协议，该结果作为环境准备事实保留，不改写 `global.json`，后续使用仓库输出目录内隔离 SDK。

隔离 SDK 通过 Microsoft `dotnet-install.ps1` 安装至 `output/core-rc/dotnet`，版本为 `10.0.300`；该目录位于构建输出范围，不改变候选指纹。

## Feature 证据矩阵

| Feature ID | 索引状态 | 详细卡状态 | 测试矩阵状态 | 证据摘要 |
| --- | --- | --- | --- | --- |
| AUC-CORE-001 | Verified | Verified | Verified | Builder、冻结和 Build 回滚合同对应 Core runtime/headless 测试。 |
| AUC-CORE-002 | Verified | Verified | Verified | Pipeline、middleware transaction 和 Host lifecycle 对应并发及 headless 测试。 |
| AUC-CORE-003 | Verified | Verified | Verified | Scope tree、停止/释放竞态对应 tree、headless 与 AOT 测试。 |
| AUC-CORE-004 | Verified | Verified | Verified | Module 合同、状态机及 lifecycle ownership 对应 runtime、并发与 AOT 测试。 |
| AUC-CORE-005 | Verified | **In Progress** | Verified | DI Attribute/owner/generated registrar 有具体测试，但三处状态不一致，Gate 失败。 |
| AUC-CORE-006 | Verified | Verified | Verified | Diagnostics 输入、并发和完成边界对应 runtime/headless/AOT 测试。 |
| AUC-CORE-007 | Verified | Verified | Verified | Dispatcher 抽象和依赖边界对应 integration 测试。 |
| AUC-CORE-008 | Verified | Verified | Verified | Generated module catalog、图验证与零副作用循环失败对应 generator/headless/AOT 测试。 |

## 门禁执行证据

### CORE-RC-003

- 锁定 SDK `10.0.300` 同时生成 `net10.0` 和 `net8.0` 的 `AtomUI.City.Core.dll` 与 XML 文档。
- `engineering/check-public-api.sh` 退出码为 0：351 个冻结签名、两个 TFM 共 620 个 XML member，nupkg 与 snupkg 创建成功。
- `ProjectDependencyBoundaryTests|PackagingReleaseGateTests` 为 15/15，通过且无跳过。
- Release 构建每个 Core TFM 均产生 `Microsoft.SourceLink.Common.targets` warning：“源代码管理信息不可用 - 生成的源链接为空”。初次联网 restore 还因本机 NuGet TLS/代理问题产生 `NU1900`；后续使用本地 package cache 和关闭联网审计完成确定性 restore。
- 退出码成功不能覆盖零 warning 合同，因此 Gate 状态为 `Failed`。SourceLink warning 还意味着当前包不能提供合格的源码映射证据。

### CORE-RC-004

- Core Release 全量测试：总计 254，通过 253，失败 1，跳过 0。
- 失败测试：`ApplicationHostBuilderTests.BuildFreezesCapturedConfigurationRootAndProviders`。
- 失败位置：测试在 Build 前向 Configuration 加入 `Root:Mode` 与 `Root:Nested:Level`，随后 `root.GetChildren()` 中找不到 `Root`。单独复跑仍失败，证明不是一次性并发抖动。
- Core 相关 Generator 专项测试：42/42，通过且无跳过。第一次构建被 Avalonia telemetry 写用户目录的沙箱权限阻断；在允许既有构建任务写入且设置 telemetry opt-out 后，测试断言全部通过。

### CORE-RC-005

以下高风险测试类在 Release、`--no-build --no-restore` 下连续运行 20 轮，每轮独立进程均在 5 分钟上限内以退出码 0 完成：

```text
ApplicationHostIndustrialLifecycleTests
ApplicationHostRuntimeTests
LifecycleMiddlewarePipelineTests
LifecycleScopeTreeTests
ModuleRegistryConcurrencyTests
HostDiagnosticsTests
```

结果为 20/20 轮通过，无挂起、超时或偶发失败。

### CORE-RC-006

MVP JIT 产品入口最终 JSON：

```json
{"scenario":"all","success":true,"exitCode":0,"selectedModuleCount":6,"selectedServiceCount":27,"permutationCount":120,"combinationCount":32,"concurrentScopeCount":64,"failures":[]}
```

Core 全量测试中的唯一失败不属于 `CoreHeadlessProcessTests` 或 `CoreMvpCliProcessTests`，真实进程用例没有报告其他失败。

### CORE-RC-007

`engineering/check-core-mvp-aot.ps1` 使用隔离 SDK 10.0.300 完成：

| TFM/RID | Restore | Publish | Native run | 产品矩阵 |
| --- | --- | --- | --- | --- |
| net10.0/win-x64 | Passed | Passed | Passed | 6/27/120/32/64，零 failure |
| net8.0/win-x64 | Passed | Passed | Passed | 6/27/120/32/64，零 failure |

脚本未发现 `IL2xxx`、`IL3xxx` 或 `will always throw`。发布过程中出现的非 AOT SourceLink warning 已归入 CORE-RC-003，不改写 AOT 风险检查结果。

### CORE-RC-008

`AtomUI.City.Core.1.0.0.nupkg` 包含两个 TFM 的 DLL、PDB、XML，以及 LICENSE、README 和 release notes。nuspec 只声明：

- `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.8`
- `Microsoft.Extensions.Hosting 10.0.8`

未包含测试、CLI、Avalonia、AtomUI 或 Roslyn runtime 依赖。

位于 `output/core-rc/consumer` 的隔离 net8 Console 消费项目清空外部 package source，仅从本地 RC 包源还原 `AtomUI.City.Core 1.0.0`，以 0 error/0 warning 编译。使用本机 net8 x64 runtime 运行后成功执行 Build、Start、Stop、Dispose 并输出 `CORE_RC_CONSUMER_OK`。首次误用只含 net10 runtime 的隔离 SDK host 时按预期报告缺少 net8 runtime；该环境选择错误未改变消费程序或包。

### CORE-RC-010

Core 文档全部相对链接可解析，所有 Markdown code fence 成对，`git diff --check -- docs/modules/core` 退出码为 0。验收结束前重新计算候选范围指纹，仍为 `178c1202bcf54fdd46b62d842587abdeb5ea4ce0f208ed621dd3311c8de3b769`，证明执行期间没有混用修改前后的候选源码。

## 性能观察

对 Release JIT MVP `verify --scenario all` 执行 10 个独立进程。第一次采集在进程退出后读取 `PeakWorkingSet64` 得到无效的 0，已明确废弃；随后改为进程存活期间每 5 ms 轮询工作集并重新采集完整 10 个样本：

| 样本 | Wall-clock (ms) | Peak observed working set (MiB) |
| ---: | ---: | ---: |
| 1 | 291.68 | 50.86 |
| 2 | 279.75 | 51.66 |
| 3 | 291.72 | 50.31 |
| 4 | 283.65 | 50.39 |
| 5 | 281.12 | 48.54 |
| 6 | 284.83 | 50.00 |
| 7 | 276.82 | 50.89 |
| 8 | 286.66 | 50.77 |
| 9 | 285.69 | 50.02 |
| 10 | 279.35 | 50.21 |

- Wall-clock min/median/max：276.82 / 284.24 / 291.72 ms。
- 工作集 min/median/max：48.54 / 50.35 / 51.66 MiB。
- 10/10 退出码为 0，全部产品 JSON 成功；没有 5 倍离群、挂起或数量级异常。
- 这是本机产品场景观察值，不是跨机器 SLA，也不替代后续独立 Benchmark 的细粒度 Build/Start/Stop、DI、Scope、Diagnostics 和模块图基线。

## 已知限制

- RC1 源码尚未形成干净、可签出的 commit；内容由 scope fingerprint 冻结，适合工程验收，不等价于可从单一 Git commit 重建的正式发布候选。
- 本机访问 NuGet 使用代理时发生 Windows TLS `SEC_E_NO_CREDENTIALS`；验收依靠已经存在的可信本地 package cache 完成，漏洞数据联网审计不属于本次成功证据。

## 发布阻断项

1. `features.md` 中 AUC-CORE-005 的详细卡为 `In Progress`，与 Feature 索引、测试矩阵及 overview 的 `8/8 Verified` 冲突。
2. `ApplicationHostBuilderTests.BuildFreezesCapturedConfigurationRootAndProviders` 在 SDK 10.0.300/Release 下确定性失败，当前 Core 自动化测试不是全绿。
3. Core Release 的 SourceLink 构建产生 warning 且生成映射为空，不满足零 warning 发布门禁；当前未提交候选的包 metadata 指向 HEAD commit，但二进制还包含未提交内容，正式发布不可复现。

## 最终结论

**Engineering RC1 不通过。**

10 个 Gate 中：6 个 `Passed`、3 个 `Failed`、1 个 `Observation`。Core 的生命周期并发、MVP 产品矩阵、双 TFM NativeAOT、Public API 内容门禁、包结构和外部消费均取得了强通过证据；但合同状态漂移、一个确定性 Core 测试失败以及 SourceLink/零 warning 发布门禁失败均属于冻结协议定义的阻断项。

本报告不把 RC1 标记为工业级发布完成。修复上述三项并形成新的候选指纹后，应按 [release-candidate-validation.md](release-candidate-validation.md) 的变更控制规则执行 RC2；不得直接把 RC1 的失败状态改写为通过。
