# AtomUI.City.EventBus Testing

本文件与 [features.md](features.md) 一一对应，是 EventBus 九个 Feature 的唯一验收矩阵。Application Plane 已验证；涉及真实 PluginSystem 的集成状态单独保留为 Pending。

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 生命周期、线程、插件、dispatcher、generator、build、NativeAOT 和真实进程行为必须有专项测试。
- 诊断测试必须断言稳定 code 和最小定位 context，不能只断言出现日志。
- 释放、取消、超时、撤销、Unload 和 Dispose 后行为必须有断言。
- 并发测试必须包含可控 barrier、高重复次数和唯一事务断言，不能用偶发 timing 作为证据。
- CLI dogfood 验证真实产品组合，但不能替代 Feature 级测试。
- Headless、NativeAOT 和外部包消费者进程必须遵守 `TEST-INFRA-001`：顶层异常写入 `stderr` 并返回非零退出码，启动器有界等待、收集双输出并在 Windows 下抑制仅属于测试子进程的系统错误框；不得吞异常或修改全局 WER。

## 完成口径

- `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 全部 `Verified`，才能认定 Application Plane MVP 通过。
- `AUC-EVENTBUS-009` 的 EventBus 侧 contract tests 在本模块完成；真实动态插件生命周期测试等待 PluginSystem，并且只有两侧证据都通过才能把 Plugin Plane 标记为 `Verified`。
- 性能 Benchmark 初期作为观察门禁；稳定基线形成后再为分配量、吞吐和复杂度趋势设置阻断阈值。

## 测试矩阵

| Feature ID | Test Type | Test Family | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Unit + RuntimeLifecycle + Dogfood | EventPublicationTests | PublishAsync/PostAsync 完成语义、EventId、correlation/causation、publish depth、订阅快照时点、无订阅者、取消和 immutable result。 | null/default 输入、非法 options/result、预取消、bus/channel 停止、Post 拒绝。 | Verified |
| AUC-EVENTBUS-002 | Unit + RuntimeLifecycle + Concurrency | EventSubscriptionLifecycleTests | 每条订阅唯一 owner；Window/Route/Application 同 event 隔离释放；强制 LifecycleScope owner；静态 ApplicationScope 和插件 Lease 绑定；六状态稳定值；注册/撤销原子性；Quiescing barrier；组合 token；in-flight drain；快速 Dispose；StopAsync/DisposeAsync 唯一终止事务。 | null/stopped owner；owner/EventBus stop 与注册提交竞争；owner cancellation 到达 handler；并发 publish/Dispose/StopAsync/DisposeAsync；等待取消或 drain timeout 后终止继续；handler 创建/释放失败；Faulted；重复调用；ownerless API 不进入目标合同。 | EventBus Contract Verified / PluginSystem Integration Pending |
| AUC-EVENTBUS-003 | Unit + Contract + Concurrency | EventContractRegistryTests; EventBusRegistrationTests | 稳定 ContractId、id/type/assembly/plane identity、精确双向匹配、排序只读快照、Register/Freeze 原子竞争、DI collected descriptor 汇入和生产冻结。 | 重复/default id、重复 type、冻结后写入/未知 contract、Shared/PluginPrivate 错误 ALC、自定义 Registry 丢失 descriptor。版本/schema/object graph 和 Plugin Private Registry 生命周期分别由 AUC-008/AUC-009 验收。 | Verified |
| AUC-EVENTBUS-004 | Unit + Threading + RuntimeLifecycle | EventDispatchAndFailurePolicyTests | Current/UiThread/Background/Serialized、Post/InlineIfAllowed、显式 dispatcher、受管后台 scheduler、delivery status、订阅级错误策略、取消、timeout、cleanup 聚合和 Post 后台错误观察。 | dispatcher 不可用、handler failure/cancellation/timeout、忽略取消的 lingering handler、StopPublication 并发边界、FailPublisher、DisableSubscription。 | Verified |
| AUC-EVENTBUS-005 | Unit + Concurrency + Diagnostics | EventDiagnosticsTests | 稳定诊断目录、因果链、最小 context、metrics snapshot、诊断限流、payload 安全投影和 sink 故障隔离。 | dropped/coalesced/rejected、递归、timeout、contract/capability 错误、诊断 sink 失败、插件对象残留。 | Verified |
| AUC-EVENTBUS-006 | Integration + RuntimeLifecycle + Headless | EventBusHostIntegrationTests | `EventBusModule` 无手工 `AddEventBus` 的 DI 闭环、普通 DI 立即可用与 Host-managed 隔离、internal lifecycle controller 不可见、Build 前后不启动、三个初始化阶段收到真实 ApplicationScope、Start 后开放、Stop/Dispose 后拒绝、Stop-before-Start、共享终止事务和 Host deadline。 | 重复注册、初始化前操作、非法 Scope、不同 Scope 重复启动、启动回滚、并发 Stop/Dispose、worker/handler 清理失败。 | Verified |
| AUC-EVENTBUS-007 | Unit + Concurrency + Stress + Benchmark | EventChannelRuntimeTests | 默认/命名 channel 隔离；Publish/Post 共享 admission；Serialized/Partitioned/Concurrent；单 runtime capacity、全局 runtime 总量、最大并发和 active partition 回收；顺序；Wait/Reject/Drop/Coalesce；指标；重入拒绝；关闭和资源回收。 | 非法 channel/options/partition/runtime 上限；重复 descriptor；queue full/closed；动态 identity 并发超限；等待取消/timeout；自身 channel await；分区队首阻塞/丢失唤醒；shutdown drain。 | Verified |
| AUC-EVENTBUS-008 | Generator + Build + Headless + NativeAOT | EventBusGeneratorTests; EventBusNativeAotProcessTests | Attribute 到唯一 Core registrar/manifest/Host 的生产闭环；同 owner 多 contribution、多程序集 registrar 去重、handler `TEvent` 到本地或引用 generated Shared catalog 的编译期闭包、实际选中 Module contribution 的 Build 闭包、封闭对象图白名单与 generated proof、稳定输出、强类型 invoker、ApplicationScope 自动释放、trimming 和双 TFM NativeAOT。 | owner 缺失/跨程序集冒充、ContractId 冲突、handler 指向无 attribute、无 manifest、manifest/registrar 不一致或未选中 Module 的 contract、`object`/interface/外部或可变对象图、手工 contract 冒充插件 Shared contract、非法 handler/channel/plane、manifest 版本不支持。 | Verified |
| AUC-EVENTBUS-009 | Contract + PluginLifecycle + ALC | EventBusPluginContractTests; EventBusPluginLifecycleTests | 受限 publisher/subscriber、capability、Shared/Private plane、领域 lease、激活回滚、三阶段 subscription admission、pending registration barrier、quiescing、单一总 deadline drain、配额和 ALC 可回收；timeout 后 Faulted、稳定异常/诊断、迟到清理观察及 PluginId 保留。 | capability denied、版本不兼容、Subscribe/Stop 竞争、diagnostics 同步重入、锁内外部调用探测、半激活、忽略 cancellation 的 handler/operation、drain timeout、timeout 后同名重入、private type/delegate/diagnostic cache 残留。 | EventBus Contract Verified / PluginSystem Integration Pending |

## 横向产品验收

统一执行入口为 `bash engineering/check-eventbus-release.sh`；它固定执行 Release 构建、范围内格式门禁、全量/专项/20 轮压力测试、双 TFM NativeAOT、Public API、本地 NuGet 外部消费、Benchmark 及候选指纹。

Application Plane MVP 还必须通过：

- 五个以上 Module、二十个以上 handler、多个 channel/partition 的真实 CLI 组合测试。
- 发布、订阅、Stop、Dispose、owner cancellation 和配置更新的高并发随机化测试。
- Host 冷启动、首次/重复 publish、队列压力、diagnostics on/off 和大规模订阅的 Benchmark。
- `net8.0` 与 `net10.0` 的 Release build、trimming 和 Windows NativeAOT 真实进程测试。
- Public API baseline、XML 文档、SourceLink 和 package validation。
- `bash engineering/check-eventbus-package-consumer.sh` 使用本地 NuGet 源、独立包缓存和无 `ProjectReference` 的 net8 外部消费者，验证打包后的 Module manifest 及 Host/EventBus 真实行为。
- 故障注入进程以非零退出码和完整异常栈结束，自动化期间不出现需要人工处理的 Windows 错误弹窗。

### Benchmark 观察门禁

首份可重复基线由 `benchmarks/AtomUI.City.EventBus.Benchmarks` 提供，使用 Release 构建执行：

```powershell
bash engineering/check-eventbus-benchmarks.sh
```

当前场景覆盖无订阅/单订阅/20 订阅 Publish、1/64/256 个已实例化 channel 下的重复 Publish、达到全局 runtime 上限后的拒绝成本、diagnostics on/off、订阅建立释放和 Wait/Reject burst。入口会拒绝零执行和无有效统计结果，并要求生成 CSV 证据。Benchmark 结果是观察数据，不提交机器相关的绝对耗时作为兼容性承诺；发布候选之间必须比较吞吐、延迟和分配量趋势，出现数量级退化时阻断并调查。

## 缺口处理

无法由当前依赖模块完成的测试必须明确登记责任方，不能用 smoke test 替代：

- UI Dispatcher 集成由 EventBus fake dispatcher 先验证合同，Presentation 建成后补真实 UI 测试。
- Plugin Plane 由 EventBus contract fake 先验证边界，PluginSystem 建成后补动态 ALC 生命周期测试。
- Generator/NativeAOT 是 Application Plane MVP 阻断门禁，不能延期为普通集成工作。
