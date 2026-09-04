# Core + EventBus Dogfood CLI 首次验证记录

验证日期：2026-09-04  
平台：Windows / win-x64  
仓库要求 SDK：10.0.300  
本次可用 SDK：10.0.111（仓库要求版本在本机缺失，未修改根 `global.json`）

## 结果

| Gate | 结果 | 证据 |
| --- | --- | --- |
| Release 编译 | Passed | net10.0，TreatWarningsAsErrors=true，零 warning/零 error |
| 完整产品场景 | Passed | `modules=10, services=31, contracts=12, handlers=20, scenarios=6` |
| 场景进程隔离 | Passed | happy-path、ordering、concurrent、failure、ownership、cancel-stop 分别独立运行 |
| 重复压力 | Passed | `verify-all` 连续 20 轮通过 |
| NativeAOT 发布 | Passed | net8.0 / win-x64 / self-contained |
| NativeAOT 真实运行 | Passed | 原生 exe 执行全部六个场景并输出匹配 JSON |
| 指定 SDK 复验 | Pending | 本机缺少根 `global.json` 锁定的 10.0.300；正式 CI 仍须在该 SDK 上执行 `verify.ps1` |

## 首轮运行发现

最初的 failure 组合让 `StopPublication` owner 延续到了后续 `DisableSubscription` 子场景，导致后者按设计被前者截断。fixture 已把两类策略拆成独立 owner 生命周期事务后通过。该发现说明联合实战场景能够捕获单功能测试不易暴露的订阅生命周期相互污染。

## 当前结论

在本次可用 SDK 和平台上，Core + EventBus 联合 dogfood 产品验证通过。由于尚未使用仓库锁定的 SDK 10.0.300 执行统一入口，本文不把“指定 SDK 复验”伪记为 Passed；这不改变已经完成的 Release、20 轮压力与 NativeAOT 功能证据。
