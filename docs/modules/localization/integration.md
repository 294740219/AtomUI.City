# AtomUI.City.Localization Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Localization | AtomUI.City.Localization 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Localization.Tests |
| AtomUI.City.State | AtomUI.City.Localization | `IStateFactory` 创建 `localization.culture` writable state，Localization 仅向消费者公开 `IReadOnlyState<CultureState>`。 | State contract -> Localization runtime -> consumer | 随 LocalizationService 创建和释放 | State 发布遵循 State 模块并发合同 | State factory 缺失时显式构造使用模块内 fallback；DI 路径使用已注册 factory。 | CultureStateTests; LocalizationRegistrationTests |
| PluginSystem | AtomUI.City.Localization | 插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Localization.Tests |
| AtomUI.City.Localization | AtomUI.City.Generators | `LanguagePackageAttribute` 与 `LocalizedResourceAttribute` 是编译期输入；Generator 输出 `GeneratedLocalizationManifest` 和原子 `RegisterPackages` 入口。 | Localization declarations -> Generator -> application Registry | build 时生成；Host 启动时显式注册 | 纯编译期确定性处理；注册同步执行 | 非法声明以稳定 build diagnostic 阻断 source 输出。 | tests/AtomUI.City.Generators.Tests |
| AtomUI.City.Localization | AtomUI.City.Presentation | `IPresentationLocalizationBridge`、`ILocalizedText` 和 `CultureState` 是桥接合同；Presentation 实现 Avalonia/AtomUI resource 与 binding adapter。 | Localization contract -> Presentation adapter；adapter 经 DI 回注 LocalizationService | bridge 随 Host；binding 随 View/Route/Window owner | Localization 不保证 UI 线程；Presentation adapter 必须 dispatch UI mutation | bridge 失败不回滚已提交 culture；返回失败 Result、诊断并继续本地文本刷新。 | LocalizationServiceTests; AtomUI.City.Presentation.Tests |
| Testing | AtomUI.City.Localization | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Localization.Tests |

## 集成硬约束

- 插件语言包卸载后不得出现在 lookup。
- Localization 只能依赖 State/Core contract，不能引用 Avalonia 或 Presentation concrete type。
- Presentation 负责 UI dispatcher；Localization 的普通 callback 不得被描述为天然运行在 UI 线程。
- Generator registrar 必须使用 Registry 原子批量入口，不能逐项发布部分 manifest。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
