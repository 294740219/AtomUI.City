# StressCli Localization 生产仿真设计

版本：v1.0
执行边界：无 GUI 的真实 City Host
目标模块：`AtomUI.City.Localization`，并联合 Core、State、EventBus、Routing、MVVM 验证

## 1. 目标

本方案把 StressCli 模拟成一个支持中英文热切换的多租户运营控制台。测试不只验证 `GetStringAsync` 返回字符串，而是让语言包、作用域、动态文案、格式化消息、路由上下文、插件撤销、故障恢复和并发加载进入同一条业务链。

所有场景运行在真实 `ApplicationHost` 和 Microsoft DI 中，不使用 GUI，也不绕过 `LocalizationService` 的生产注册路径。任一不变量失败时 CLI 返回非零退出码。

## 2. 规模

| 对象 | 规模 | 说明 |
| --- | ---: | --- |
| Culture | 4 | `en-US`、`en`、`zh-CN`、`zh-Hans` |
| LanguagePackageDescriptor | 30 | Host、Presentation、Module、Route、Window、Plugin 六类 scope |
| 不同业务文案 key | 至少 120 | 菜单、命令、状态、错误、订单、支付、支持、路由、窗口和插件文案 |
| 语言包资源项 | 至少 250 | 同一 key 在不同 culture/scope 下具有独立值 |
| 活动 scope | 10 | 3 Module/Plugin/Window + 7 Route/组合上下文 |
| 动态 `ILocalizedText` | 功能阶段 6 个，soak 阶段 12 个 | 普通文本和格式化消息同时刷新 |
| 功能不变量 | 12 | `I19` 到 `I30` |
| Soak 轮次 | 300 | 每轮切换 culture、导航、并发 lookup、格式化和 EventBus 通知 |

## 3. 语言包拓扑

```text
Host.Core                 en-US -> en, zh-CN -> zh-Hans，另有 en/zh-Hans parent 包
Presentation.Shell        en-US, zh-CN
Module.Operations         en-US -> en, zh-CN -> zh-Hans，另有 en/zh-Hans parent 包
Module.Billing            en-US, zh-CN
Module.Support            en-US, zh-CN
Route.Orders              en-US, zh-CN
Route.Payments            en-US, zh-CN
Route.Search              en-US, zh-CN
Route.Support             en-US, zh-CN
Route.Reports             en-US, zh-CN
Window.Main               en-US, zh-CN
Window.Export             en-US, zh-CN
Plugin.Sales              en-US, zh-CN，contribution=fixtures.plugin.sales.localization
```

合计 30 个 descriptor。`Host` 和 `Presentation` 全局可见；其余 descriptor 必须同时满足 scope lease 存活和 `LocalizationLookupContext` id 匹配。

## 4. 文案业务模型

文案按真实运营软件用途分组：

- 全局外壳：菜单、保存、删除、查询、刷新、登录状态、网络错误和权限错误；
- Presentation：主题、窗口、导航、对话框、通知区域和布局方向；
- 运营中心：订单处理、批量操作、导出、审计、任务状态和兼容提示；
- 财务：结算、税额、退款、支付状态、发票和金额格式化；
- 客服：工单、优先级、分配、回复、关闭和 SLA；
- Route：Orders、Payments、Search、Support、Reports 的标题、描述、空状态、过滤器、动作和状态；
- Window：主窗口、导出窗口及其独立命令；
- Plugin：销售插件看板、预测、佣金、导出和插件状态。

多个 scope 有意覆盖 `Common.Save`、`Common.Export` 和 `Route.ContextMarker`，用于验证确定性的 scope priority 与 context 隔离。

## 5. Phase J：完整业务矩阵

| 不变量 | 场景 | 必须证明 |
| --- | --- | --- |
| I19 | Catalog 完整性 | 30 个 descriptor、至少 120 个 key、至少 250 个资源项，六类 scope 均存在 |
| I20 | Manifest-only startup | Host 启动后 loaded package 为空；首次全局查找不加载 scoped package |
| I21 | Scope priority | Route > Window > Plugin > Module > Host > Presentation；无 lease 时退回全局资源 |
| I22 | Context isolation | 多个 Route 同时 active 时只命中当前 context 的 `Route.ContextMarker` |
| I23 | Recursive fallback | `en-US -> en`、`zh-CN -> zh-Hans` 的 scoped parent key 正确命中 |
| I24 | Dynamic text | culture 切换后普通文本与格式化文本按同一 revision 刷新，不出现半中文半英文 |
| I25 | Concurrent load | 64 个首次并发 lookup 合并为同一个 `(culture, packageId)` provider load |
| I26 | Pre-commit failure | provider 失败和调用方取消都保留旧 culture，不发布部分状态 |
| I27 | Post-commit completion | bridge 内取消调用方 token 后，已提交 culture、bridge 和全部文本刷新仍完成 |
| I28 | Bridge failure | Presentation apply 失败不回滚 culture，返回失败 Result、继续刷新并产生诊断 |
| I29 | Plugin revoke | contribution 撤销后旧文本立即 fallback，重复撤销为 0，cache/registry 不复活 |
| I30 | Diagnostics and release | missing、format、load、switch reject、bridge、revoke 诊断齐全；lease/text Dispose 后不再回调 |

## 6. Phase K：高频联合 Soak

每轮执行：

```text
SetCulture(en-US/zh-CN alternating)
-> CultureState subscriber observes one revision
-> 12 LocalizedText handles refresh
-> Router navigates Orders/Payments/Reports/Search
-> 16 parallel lookup requests use route/module/window/plugin contexts
-> formatted order/payment/support messages render
-> EventBus publishes SettingsChanged(culture)
-> invariant checks current culture, route title and text family
```

300 轮至少产生：

- 300 次有效 culture commit；
- 3,600 次动态文本刷新；
- 4,800 次并发 lookup；
- 300 次 Router navigation；
- 300 次 EventBus 通知；
- 600 次格式化消息；
- 多次 scope lease 释放和重新激活。

结束时释放全部 lease、`ILocalizedText`、EventBus owner 和 DI scope，再执行一次 culture switch，证明无释放后回调；Host 必须确定性 Stop/Dispose。

## 7. 故障注入

`StressLanguagePackageProvider` 是可计数、可延迟、可单次失败的 InMemory provider，用于观察真实 service cache/in-flight 行为。`StressPresentationLocalizationBridge` 可以记录 state、单次失败，并在 apply callback 内取消发起方 token。

故障只通过公开 contract 注入：

- provider 返回 `PackageLoadFailed`；
- provider 延迟期间调用方取消；
- bridge 返回 `PresentationApplyFailed`；
- formatter 抛出异常；
- lookup 缺失 key；
- plugin contribution 在文本仍被持有时撤销。

## 8. 命令和验收

```text
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -- localization
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -- localization-soak
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -- run-all
```

冻结门禁：三条命令均返回 0；Localization、State、Presentation、Generators 专项测试继续全绿；fixture Release 构建零 warning/零 error；同一进程和多进程重复执行结果确定。
