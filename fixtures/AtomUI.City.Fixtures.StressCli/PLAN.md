# AtomUI.City 六模块联合实战验证应用

版本：v3.0
应用名：AtomUI City Operations
性质：无 GUI、可重复运行、失败即返回非零退出码的生产形态 CLI fixture

## 1. 目标

本应用模拟一个多租户全渠道商业运营后台，在同一个真实 Host 中联合使用：

- `AtomUI.City.Core`：模块依赖图、DI、生命周期、诊断、并发启停和失败补偿；
- `AtomUI.City.EventBus`：36 类业务事件、同步与后台投递、优先级、失败隔离和取消；
- `AtomUI.City.State`：54 个注册状态、10 个计算状态、8 个集合状态、权限、快照和并发；
- `AtomUI.City.Mvvm`：16 个 ViewModel、命令、交互、激活与释放；
- `AtomUI.City.Router`：30 条生成路由、匹配策略、守卫、解析器、中间件、并发导航、历史和动态贡献。
- `AtomUI.City.Localization`：30 个语言包 descriptor、至少 120 个业务文案 key、六类资源 scope、中英文热切换、fallback、动态文本、插件撤销和并发加载。

它不是 API 冒烟样例。每一种能力必须进入跨模块业务链，并由可执行不变量证明结果。

## 2. 规模硬门槛

| 对象 | 数量 | 计数口径 |
|---|---:|---|
| 业务 Module | 40 | 不含框架自带 `EventBusModule`、`RoutingModule` |
| 业务 Service | 61 | 不含框架设施和 4 个 Router 管道组件 |
| EventBus 契约 | 36 | 每类有稳定 contract name 与 owner |
| State | 72 | 54 个 registry state + 10 个 computed + 8 个 collection |
| ViewModel | 16 | 均使用命令、交互、激活作用域、State 和 EventBus |
| 静态路由 | 30 | 全部由 RouteMap generator 生成 |
| 模块依赖边 | 78 | 必须可拓扑排序并包含多级钻石依赖 |
| Localization 语言包 | 30 | 4 个 culture、六类 scope、至少 120 个不同文案 key |
| 验收不变量 | 30 | I01-I18 为原五模块基线，I19-I30 为 Localization 仿真；任一失败返回非零退出码 |

## 3. 业务边界与模块图

### 3.1 40 个模块

```text
L0  Foundation, Telemetry, Settings
L1  DataCatalog, Messaging, Security, Scheduling, Identity, Tenancy
L2  TelemetryArchive, CatalogIndex, Orders, Inventory, Customers, Product
L3  Billing, Reporting, BillingTax, Pricing, Search, Fraud, Fulfillment
L4  Analytics, Notifications, Audit, Shipping, Promotions, Payments
L5  Dashboard, Returns, Recommendations, Support, Workflow, Workspace
L6  Shell, StoreFront, Navigation
L7  Operations
Chaos Faulty, Flaky
```

完整依赖边：

```text
DataCatalog->Foundation                 Messaging->Foundation
Security->Foundation                    Scheduling->Foundation,Telemetry
Identity->Foundation,Security           Tenancy->Foundation,Settings
TelemetryArchive->Telemetry             CatalogIndex->DataCatalog
Orders->DataCatalog,Messaging            Inventory->DataCatalog
Customers->DataCatalog,Messaging         Product->DataCatalog,CatalogIndex
Billing->Orders,Security                 Reporting->Scheduling,Telemetry
BillingTax->Billing                      Pricing->Product,Settings
Search->Product,Telemetry                Fraud->Orders,Security,Telemetry
Fulfillment->Orders,Inventory,Product    Analytics->Orders,Inventory,Reporting
Notifications->Messaging,Customers       Audit->Billing,Reporting,Security
Shipping->Fulfillment,Scheduling         Promotions->Pricing,Customers
Payments->Billing,Security,Fraud         Dashboard->Analytics,Notifications
Returns->Orders,Inventory,Payments       Recommendations->Search,Analytics,Customers
Support->Audit,Customers,Messaging       Workflow->Audit,Scheduling,Messaging
Workspace->Dashboard,Billing,Audit       Shell->Workspace
StoreFront->Workspace                    Navigation->Shell,Workspace,Dashboard
Operations->Navigation,Payments,Returns,Recommendations,Support,Workflow
Faulty->Foundation                       Flaky->Foundation
```

每个模块继承 `ModuleBase`，服务注册在模块配置阶段完成；模块启动和停止均写入全局生命周期账本。`Faulty` 与 `Flaky` 提供可控启动、停止故障。

## 4. 服务设计

61 个业务服务按真实生命周期分布：36 Singleton、15 Scoped、10 Transient。
混沌控制用的 `IFaultInjector`、框架基础设施及 4 个 Router 管道组件不计入 61 个业务服务。

| 领域 | 服务 |
|---|---|
| 基础设施 | IClockService, ISequenceService, IEnvironmentProbe, ICheckpointService, ITelemetrySink, IMetricsCollector, ISettingsStore |
| 目录与消息 | IDataCatalog, ICatalogIndex, ICatalogValidator, IMessageCodec, IMessageJournal, IMessageDeduper, IMessageQueue, ISecurityBoundary |
| 商业核心 | IScheduler, IScheduleStore, IOrderPolicy, IOrderPricing, IOrderValidator, IStockPolicy, ICustomerDirectory, IBillingCalculator, IBillingLedger, ITaxPolicy |
| 洞察与外壳 | IReportBuilder, IReportFormatter, IAnalyticsEngine, ITrendDetector, INotificationDispatcher, IAuditTrail, IDashboardAggregator, IWorkspaceStore, IShellNavigator, IStoreFrontFacade, IFlakyProbe |
| 身份与租户 | IIdentityDirectory, ITokenIssuer, ITenantDirectory |
| 商品与价格 | IProductCatalog, IProductReader, IPriceBook, IPromotionEngine |
| 履约与物流 | IFulfillmentPlanner, IPickListStore, IShippingQuote, IShipmentTracker |
| 支付与退货 | IPaymentGateway, IPaymentLedger, IReturnPolicy, IReturnCaseStore |
| 搜索与推荐 | ISearchIndex, ISearchSession, IRecommendationEngine |
| 风险与协作 | IFraudScorer, ISupportDesk, ISupportSession, IWorkflowEngine, IWorkflowRun |
| 导航与编排 | INavigationAudit, IOperationsFacade |

核心跨域调用：`OperationsFacade -> WorkflowEngine -> PaymentGateway -> FraudScorer -> OrderStore`，并同时读取租户、价格、库存、履约、搜索和支持域数据。Scoped 服务必须在两个 scope 中证明同 scope 同实例、跨 scope 不同实例；Transient 必须证明每次解析不同实例。

## 5. EventBus 设计

36 个契约：

```text
OrderSubmitted, OrderConfirmed, InventoryReserved, InventoryLow,
CustomerRegistered, BillingSettled, TaxComputed, ReportGenerated,
AnalyticsRefreshed, NotificationDispatched, AuditAppended, DashboardUpdated,
TelemetrySampled, ScheduleFired, SettingsChanged, CatalogRebuilt,
MessageJournaled, WorkspaceSaved, SecurityCrossed, FaultInjected,
UserSignedIn, TenantSwitched, ProductIndexed, PriceChanged,
PromotionApplied, FulfillmentPlanned, PickListCreated, ShippingQuoted,
ShipmentDispatched, PaymentAuthorized, PaymentCaptured, ReturnRequested,
SearchExecuted, RecommendationProduced, FraudFlagged, SupportTicketOpened
```

所有契约使用稳定名称 `fixtures.events.*`。处理链包含优先级、并行订阅、后台投递、取消、一次性订阅、异常处理器和释放后的静默退订。订单主链至少穿过 12 个不同事件类型。

## 6. State 设计

### 6.1 54 个注册状态

原有 32 个状态覆盖 Host、订单、库存、客户、账单、报表、通知、审计、工作区和 Shell。新增 22 个：

```text
IdentitySession, IdentityFailedLogins, TenantCurrent, TenantRevision,
ProductCount, PricingCurrency, PricingRevision, PromotionsApplied,
FulfillmentPending, FulfillmentCompleted, ShippingQuoted, ShippingInTransit,
PaymentsAuthorized, PaymentsCaptured, ReturnsOpen, SearchQueries,
RecommendationsGenerated, FraudScore, FraudFlagged, SupportOpenTickets,
WorkflowRunning, NavigationCurrentRoute
```

访问策略必须同时覆盖 `ReadOnly`、`HostWrite`、`OwnerWrite`、`AuthorizedWrite` 和 `PluginIsolated`。

### 6.2 10 个计算状态

`CounterDouble`、`CounterPlusOne`、`CounterText`、`CommerceHealth`、`PaymentExposure`、`FulfillmentPressure`、`SearchYield`、`SupportLoad`、`OperationsScore`、`NavigationTitle`。

测试失效传播、菱形依赖、循环防护、首次计算失败、恢复后重算和释放后读取合同。

### 6.3 8 个集合状态

Orders、Inventory、Customers、Audit、Notifications、Telemetry、NavigationJournal、WorkflowRuns。覆盖批量更新、版本、快照、恢复、重复 key、并发读取和变更通知。

## 7. MVVM 设计

16 个 ViewModel：Dashboard、Orders、Inventory、Customers、Billing、Reports、Notifications、Audit、Workspace、Shell、Products、Payments、Fulfillment、Search、Support、Workflow。

每个 ViewModel 必须使用可观察属性、成功与故障命令、确认交互、激活作用域、State 与 EventBus；停用后必须不再接收通知，并且能由 Router 解析、激活并执行命令。

## 8. Router 设计

30 条生成路由：

```text
1 Shell(layout)                 2 Dashboard(index)
3 Commerce(group)               4 Orders
5 OrderDetails({id:int:min(1)}) 6 Inventory
7 Customers                     8 Billing
9 Payments                     10 Returns
11 Catalog(group)              12 Products
13 ProductDetails({sku:regex}) 14 Pricing
15 Promotions                  16 Operations(group)
17 Fulfillment                 18 Shipping
19 Reports                     20 Analytics
21 Audit                       22 Support
23 Workflow                    24 Settings
25 Search({term})              26 PremiumSearch({query}, policy)
27 Recommendations             28 Notifications(named outlet side)
29 LegacyOrders(redirect)      30 OperationsExtensionPoint
```

必须验证 generator manifest、路由名称和稳定 ID、类型参数绑定、同形模板策略裁决、守卫拒绝、resolver 预取、中间件顺序、重定向、命名 outlet、历史 journal、`Queue`/`CancelPrevious`/`RejectIfBusy` 并发模式，以及动态 contribution attach/revoke。

Localization 的完整业务与压力设计见 [LOCALIZATION-STRESS-PLAN.md](LOCALIZATION-STRESS-PLAN.md)。

## 9. 十一阶段执行模型

| 阶段 | 内容 |
|---|---|
| A | 构建真实 Host，验证 40 模块、78 条依赖边、拓扑启动和逆序停止 |
| B | 解析 61 个服务，验证三种 DI 生命周期和跨模块调用 |
| C | 注册并投递 36 个事件契约，验证顺序、并发、取消、异常与退订 |
| D | 操作 72 个 State，验证权限、计算、集合、快照和并发 |
| E | 激活 16 个 ViewModel，运行命令与交互，再停用并检查资源释放 |
| F | 执行订单到支付、履约、通知、审计、看板的联合业务链 |
| G | 注入启动、停止、handler、computed、command 故障并检查诊断和补偿 |
| H | 对 30 条静态路由和动态贡献执行完整导航矩阵 |
| I | 重复执行业务工作流、并发导航、状态恢复和 Host 启停，检查泄漏与确定性 |
| J | 执行 Localization 功能矩阵：懒加载、scope、fallback、动态文本、故障、取消、撤销和诊断 |
| K | 300 轮 Localization + State + EventBus + Router 联合 soak，检查顺序、并发和资源收束 |

命令行入口：

```text
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- run-all
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- phase h
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- routes
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- workflow
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- chaos
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- soak
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- localization
dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- localization-soak
```

## 10. 验收不变量

| ID | 不变量 |
|---|---|
| I01 | 40 个业务模块恰好各启动、停止一次 |
| I02 | 78 条依赖边均满足先依赖后模块启动、先模块后依赖停止 |
| I03 | 61 个业务服务全部可解析，生命周期符合声明 |
| I04 | 36 个事件契约全部可发布并被正确 handler 观察 |
| I05 | 同一事件的优先级顺序与后台等待语义确定 |
| I06 | handler 失败被诊断且不破坏独立订阅者 |
| I07 | 54 个 registry state 均可创建并遵守访问策略 |
| I08 | 10 个 computed state 正确失效、重算并阻止循环崩溃 |
| I09 | 8 个 collection state 的版本、快照和通知一致 |
| I10 | 16 个 ViewModel 的激活、命令、交互和停用均完整 |
| I11 | ViewModel 停用后无 State/EventBus 残留回调 |
| I12 | 生成 manifest 恰好提供 30 条静态路由 |
| I13 | Router 管道按 policy→middleware-enter→leave/enter guard→resolver→middleware-exit 顺序生效 |
| I14 | 路由并发模式、重定向、命名 outlet 和 journal 符合合同 |
| I15 | 动态路由 contribution 可见，lease revoke 后立即不可见 |
| I16 | 联合订单工作流跨越五个框架模块并得到一致最终状态 |
| I17 | 所有可控故障均产生预期结果，Host 仍可确定性停止或补偿 |
| I18 | soak 执行后无活动订阅、导航事务、作用域或未观察异常泄漏 |
| I19-I30 | Localization catalog、懒加载、scope priority/context、fallback、动态刷新、并发 load、提交边界、bridge failure、插件撤销、诊断与释放满足详细方案 |

## 11. 完成定义

1. `Release` 构建零错误、零本 fixture 引入的警告；
2. `run-all`、`routes`、`workflow`、`chaos`、`soak` 均退出码 0；
3. Core、EventBus、State、Mvvm、Router、Localization 及相关 Generator/Presentation 测试全绿；
4. 规模计数由运行时代码断言，不以注释或文档计数代替；
5. 任何实现偏离必须先更新本文档并给出理由。
