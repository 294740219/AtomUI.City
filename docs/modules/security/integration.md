# AtomUI.City.Security Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Security | Security 编译引用 Core 的 `IHostDiagnostics`；应用在 Host 服务配置阶段调用 `AddSecurity`。Core 不反向引用 Security。 | Security -> Core contract；App composition -> Security | DI singleton -> Host dispose | Security 不持有 Core lock | 诊断写入失败不改变 Security result。 | SecurityRegistrationTests; diagnostics tests |
| Routing | AtomUI.City.Security | Security 编译引用 Routing 的 `IRouteEnterGuard`、`RouteGuardContext` 和 `RouteGuardResult`；Routing 不反向引用 Security。 | Security -> Routing contract | 每次导航事务调用 guard | async + navigation cancellation token | 返回 Allow/Reject/Redirect/Cancel/Failed，不直接导航。 | RouteAuthorizationGuardTests |
| AtomUI.City.Security | Data | Data 编译引用 `IAccessTokenProvider` 和 `AccessTokenResult`。 | Data -> Security | 每次 transport 前获取 token | async + request cancellation token | Failed/Unavailable 映射为 credential unavailable。 | AccessTokenCredentialProviderTests |
| AtomUI.City.Security | Application / MVVM / Presentation | 应用命令适配器消费 `ICommandAuthorizationSource`；Security 不引用 Presentation 或 Avalonia。 | Consumer -> Security | source 为 Host singleton，consumer 订阅并释放自身订阅 | source notification 可来自后台，UI 调度由 consumer 负责 | Challenge/Denied/Forbidden/Failed 均不可执行。 | CommandAuthorizationSourceTests |
| PluginSystem (Planned) | AtomUI.City.Security | 当前只提供 contribution id 撤销和 tombstone；ContributionLease、capability 和 manifest 编排尚无 Security Feature ID。 | Future Plugin owner -> Security registry/provider | 未来 load/enable/disable/unload | 未来插件任务可取消 | 当前不能宣称完整插件生命周期集成。 | Future feature tests |
| AtomUI.City.Security | Application-owned State bridge (Planned) | State 可同步非敏感认证派生数据，但 Security 不编译引用 State，State 也不是认证权威。 | Application bridge: Security -> State value | 由应用 bridge owner 管理 | 按 State dispatcher policy | token/完整 principal 不得写入 State。 | AUC-SECURITY-008/009 leakage tests |
| Testing | AtomUI.City.Security | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Security.Tests |

## 集成硬约束

- Security 不实现登录 UI。
- 授权评估不操作 UI 或导航。
- Security 当前只编译依赖 Core 与 Routing；不得因文档中的未来集成保留无使用的 State、Data、Presentation 或 PluginSystem 项目引用。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
