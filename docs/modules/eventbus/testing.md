# AtomUI.City.EventBus Testing

本文件与 [features.md](features.md) 一一对应，是 EventBus 九个 Feature 的唯一验收矩阵。EventBus 当前处于设计阶段，矩阵状态均为 `In Design`。

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 生命周期、线程、插件、dispatcher、generator、build、NativeAOT 和真实进程行为必须有专项测试。
- 诊断测试必须断言稳定 code 和最小定位 context，不能只断言出现日志。
- 释放、取消、超时、撤销、Unload 和 Dispose 后行为必须有断言。
- 并发测试必须包含可控 barrier、高重复次数和唯一事务断言，不能用偶发 timing 作为证据。
- CLI dogfood 验证真实产品组合，但不能替代 Feature 级测试。

## 完成口径

- `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 全部 `Verified`，才能认定 Application Plane MVP 通过。
- `AUC-EVENTBUS-009` 的 EventBus 侧 contract tests 在本模块完成；真实动态插件生命周期测试等待 PluginSystem，并且只有两侧证据都通过才能把 Plugin Plane 标记为 `Verified`。
- 性能 Benchmark 初期作为观察门禁；稳定基线形成后再为分配量、吞吐和复杂度趋势设置阻断阈值。

## 测试矩阵

| Feature ID | Test Type | Test Family | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Unit + RuntimeLifecycle + Dogfood | EventPublicationTests | PublishAsync/PostAsync 完成语义、EventId、correlation/causation、publish depth、订阅快照时点、无订阅者、取消和 immutable result。 | null/default 输入、非法 options/result、预取消、bus/channel 停止、Post 拒绝。 | In Design |
| AUC-EVENTBUS-002 | Unit + RuntimeLifecycle + Concurrency | EventSubscriptionLifecycleTests | 每条订阅唯一 owner；Window/Route/Application 同 event 隔离释放；强制 LifecycleScope owner；静态 ApplicationScope 和插件 Lease 绑定；六状态稳定值；注册/撤销原子性；Quiescing barrier；组合 token；in-flight drain；快速 Dispose；StopAsync/DisposeAsync 唯一终止事务。 | null/stopped owner；owner/EventBus stop 与注册提交竞争；owner cancellation 到达 handler；并发 publish/Dispose/StopAsync/DisposeAsync；等待取消或 drain timeout 后终止继续；handler 创建/释放失败；Faulted；重复调用；ownerless API 不进入目标合同。 | In Design |
| AUC-EVENTBUS-003 | Unit + Contract + PluginContract | EventContractRegistryTests | 稳定 ContractId、版本/type/schema/assembly identity、精确匹配、配置冻结、动态 snapshot 和 Shared/Private plane 隔离。 | 重复/default id、重复 type、版本不兼容、错误 ALC、私有对象图进入 Shared Plane。 | In Design |
| AUC-EVENTBUS-004 | Unit + Threading + RuntimeLifecycle | EventDispatchAndFailurePolicyTests | Current/UiThread/Background/Serialized、显式 dispatcher、delivery status、错误策略优先级、取消、timeout、cleanup 聚合和 Post 后台错误观察。 | dispatcher 不可用、handler failure/cancellation/timeout、StopPublication 并发边界、FailPublisher、DisableSubscription。 | In Design |
| AUC-EVENTBUS-005 | Unit + Concurrency + Diagnostics | EventDiagnosticsTests | 稳定诊断目录、因果链、最小 context、metrics snapshot、诊断限流、payload 安全投影和 sink 故障隔离。 | dropped/coalesced/rejected、递归、timeout、contract/capability 错误、诊断 sink 失败、插件对象残留。 | In Design |
| AUC-EVENTBUS-006 | Integration + RuntimeLifecycle + Headless | EventBusHostIntegrationTests | Module/DI 注册、配置冻结、internal lifecycle controller 隔离、Build/Start/Stop/Dispose、Stop-before-Start、共享终止事务和 Host deadline。 | 重复注册、非法配置、启动回滚、并发 Stop/Dispose、worker/handler 清理失败。 | In Design |
| AUC-EVENTBUS-007 | Unit + Concurrency + Stress + Benchmark | EventChannelRuntimeTests | Serialized/Partitioned/Concurrent、有界 capacity、最大并发/partition、顺序、Wait/Reject/Drop/Coalesce、重入拒绝、关闭和资源回收。 | queue full/closed、等待取消/timeout、serialized 自等待、partition 泄漏、shutdown drain。 | In Design |
| AUC-EVENTBUS-008 | Generator + Build + Headless + NativeAOT | EventBusGeneratorTests; EventBusNativeAotProcessTests | Attribute 到 registrar/manifest/Host 的生产闭环、多程序集 catalog、稳定输出、强类型 invoker、trimming 和双 TFM NativeAOT。 | owner 缺失/冒充、ContractId/registrar 冲突、非法对象图/plane/capability、manifest 版本不支持。 | In Design |
| AUC-EVENTBUS-009 | Contract + PluginLifecycle + ALC | EventBusPluginContractTests; EventBusPluginLifecycleTests | 受限 publisher/subscriber、capability、Shared/Private plane、领域 lease、激活回滚、quiescing/drain、配额和 ALC 可回收。 | capability denied、版本不兼容、半激活、drain timeout、private type/delegate/diagnostic cache 残留。 | In Design |

## 横向产品验收

Application Plane MVP 还必须通过：

- 五个以上 Module、二十个以上 handler、多个 channel/partition 的真实 CLI 组合测试。
- 发布、订阅、Stop、Dispose、owner cancellation 和配置更新的高并发随机化测试。
- Host 冷启动、首次/重复 publish、队列压力、diagnostics on/off 和大规模订阅的 Benchmark。
- `net8.0` 与 `net10.0` 的 Release build、trimming 和 Windows NativeAOT 真实进程测试。
- Public API baseline、XML 文档、SourceLink 和 package validation。

## 缺口处理

无法由当前依赖模块完成的测试必须明确登记责任方，不能用 smoke test 替代：

- UI Dispatcher 集成由 EventBus fake dispatcher 先验证合同，Presentation 建成后补真实 UI 测试。
- Plugin Plane 由 EventBus contract fake 先验证边界，PluginSystem 建成后补动态 ALC 生命周期测试。
- Generator/NativeAOT 是 Application Plane MVP 阻断门禁，不能延期为普通集成工作。
