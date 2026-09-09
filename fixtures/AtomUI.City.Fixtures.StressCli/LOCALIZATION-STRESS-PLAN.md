# StressCli Localization 生产仿真设计

版本：v2.0
执行边界：无 GUI 的真实 City Host
目标模块：`AtomUI.City.Localization`，并联合 Core、State、EventBus、Routing、MVVM 验证

## 1. 目标

StressCli 模拟支持多语言热切换的多租户运营控制台。测试覆盖语言包、作用域、动态文案、格式化、路由上下文、动态贡献、故障恢复、并发加载、生命周期和资源释放，不把 `GetStringAsync` 的 happy path 当作完成标准。

所有业务场景运行在真实 `ApplicationHost` 和 Microsoft DI 中，不绕过 `LocalizationService` 的生产注册路径。每项能力由不变量判定，任一不变量失败时 CLI 返回非零退出码。

## 2. 固定规模

| 对象 | 规模 | 说明 |
| --- | ---: | --- |
| Culture | 11 | `en-US/en`、`zh-CN/zh-Hans`、`fr-FR/fr`、`zh-TW/zh-Hant`、`de-DE`、`ja-JP`、`ar-SA` |
| 启动 descriptor | 93 | Host、Presentation、Module、Route、Window、Plugin 六类 scope |
| 不同业务 key | 388 | 菜单、命令、状态、错误、订单、支付、库存、支持、报表、审计等 |
| 启动资源项 | 2,055 | 不同 culture/scope 的独立文案 |
| 真实 Provider 包 | 4 | Generator 注册的 Assembly 双语言包与 File 双语言包 |
| 动态 `ILocalizedText` | 96 | Chaos 阶段同时刷新和主动 Refresh |
| 并发 load waiter | 64 | 四分之一调用方在共享 load 中途取消 |
| 操作轨迹 | 512 | 失败时输出最后 512 条带 worker/operation 的可复现轨迹 |

## 3. 资源拓扑

固定资源包括 Host、Presentation、Operations、Billing、Support、Orders、Payments、Search、Reports、MainWindow、ExportWindow 和 SalesPlugin。扩展 culture 覆盖 Host、Presentation、三个 Module、三个 Route 和 MainWindow。

`fr-FR -> fr`、`zh-TW -> zh-Hant` 有意把部分 key 只放在父包，验证动态 scoped fallback。Route、Window、Plugin、Module 使用相同 marker 验证以下稳定优先级：

```text
Route > Window > Plugin > Module > Host > Presentation
```

Assembly 语言包由 `[LanguagePackage]`、`[LocalizedResource]` 和 Generator 产生 `GeneratedLocalizationManifest`；File 语言包从构建输出目录读取，声明 allowed root、version 和运行时计算的 SHA-256。

## 4. 执行阶段

| Phase | 场景 | 关键证明 |
| --- | --- | --- |
| J | 基础合同矩阵 | lazy load、scope、fallback、动态文本、取消、Bridge 失败、撤销和诊断 |
| K | 六模块 Soak | culture → State → text → Router → lookup → message → EventBus → MVVM 完整闭环 |
| L | 真实 Provider | Generator、Assembly、File、checksum、路径约束、scope priority 和运行时撤销 |
| M | 确定性竞态 | switch/switch、共享 load/cancel、load/revoke、lookup/lease、callback/dispose、mutation/service dispose |
| N | Seeded Chaos | 多 worker 随机交叉执行查找、切换、导航、事件、状态、动态贡献、取消和 Provider 故障 |
| O | 生命周期 | 多轮完整 Host start/stop/dispose、并发关闭、在途 load 收束、释放后合同和弱引用回收 |

Phase M 使用显式 `TaskCompletionSource` gate 控制交错，不依赖线程调度偶然命中。Phase N 每个 worker 使用由 `seed + worker` 派生的独立随机序列；异常报告包含 seed 和最后操作轨迹。

## 5. 压力档位

| Profile | Soak iterations | Chaos operations | Workers | Host cycles | Race repeats | Phase timeout |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| quick | 100 | 5,000 | 8 | 2 | 25 | 1 min |
| standard | 1,000 | 50,000 | 32 | 10 | 100 | 5 min |
| extreme | 5,000 | 100,000 | 64 | 30 | 250 | 15 min |

可使用 `--operations`、`--workers`、`--timeout` 覆盖对应值，使用 `--seed` 固定 Chaos 序列。默认 seed 为 `20260908`。

## 6. 故障注入

故障仅通过公开 contract 和 fixture provider/bridge 注入：

- provider 返回 `PackageLoadFailed`、直接抛异常、延迟或忽略取消；
- 64 个 waiter 共享 load 时独立取消；
- Bridge 阻塞、返回失败、取消调用方或在 mutation callback 中重入；
- contribution 在 package load 未完成时撤销；
- `ILocalizedText` handler 阻塞，同时从外部 Dispose；
- Host Dispose 遇到忽略取消的在途 load；
- File provider 遇到错误 checksum、越界路径和缺失文件。

## 7. CLI

```text
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net10.0 -- localization-suite --profile quick
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net8.0 -- localization-suite --profile standard --seed 20260908
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f net10.0 -- localization-extreme --seed 20260909
```

专项命令为 `localization`、`localization-soak`、`localization-providers`、`localization-races`、`localization-chaos` 和 `localization-lifecycle`。`phase <j-o>` 保留用于定位单一阶段。

退出码：`0` 全部通过；`1` 异常或 watchdog 超时；`2` 参数错误；`3` 不变量失败。

## 8. 冻结门禁

- `net8.0`、`net10.0` 的 standard suite 均返回 0；
- `net10.0` 使用 `20260908`、`20260909`、`20260910` 三个 seed 执行 extreme；
- 两个目标框架分别执行 `run-all`，standard suite 另做十次独立进程重复；
- Localization、Core、State、EventBus、Routing、MVVM、Generators、Presentation 专项测试全绿；
- fixture Release 构建零 warning/零 error，`git diff --check` 和仓库治理门禁通过；
- 最终无未观察任务异常、无在途 provider/bridge、Dispose 后无 callback，动态文案统一收敛到同一 revision。

本夹具证明 Localization 引擎及无 GUI 跨模块集成，不替代 Avalonia VisualTree 的实际刷新测试。
