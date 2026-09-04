# AtomUI.City.EventBus 发布候选收口报告

## 当前结论

- 复核日期：2026-09-04
- 验收协议：[release-candidate-validation.md](release-candidate-validation.md)
- 当前分支：`develop`
- 正式候选 commit：`25ce5d5d82f73e1adf276f683bc710c16c4df06f`
- 候选范围指纹：`8d272d10f1bd03ce5ffdaa48d237bf702773a7dbee61d9362e789095620a3db0`（243 个文件；报告自身不计入）
- Application Plane MVP：**模块内工业级验收已通过**
- Plugin Plane：**EventBus 侧 contract 已验证；真实 PluginSystem 集成 Pending**
- 正式 RC 身份：**已建立**。上述 commit 在 clean worktree 上完成统一 EventBus RC 门禁复验。

这里的“Application Plane MVP 通过”只表示 EventBus 模块自身的设计、实现和模块级验收闭环，不表示整个 City 已完成，也不把尚未施工的 PluginSystem 集成虚报为完成。

## 当前 Gate 状态

| Gate ID | 状态 | 当前证据 |
| --- | --- | --- |
| EVENTBUS-RC-001 | Passed | commit `25ce5d5d82f73e1adf276f683bc710c16c4df06f` 在 clean worktree 上复验；243 文件范围指纹为 `8d272d10f1bd03ce5ffdaa48d237bf702773a7dbee61d9362e789095620a3db0`。 |
| EVENTBUS-RC-002 | Passed | `features.md`、详细 Feature 卡、`testing.md` 和 `overview.md` 已统一 Application Plane 与 Plugin Plane 状态。 |
| EVENTBUS-RC-003 | Passed | net8.0/net10.0 Release 零 warning/error；498 项 Public API baseline；PublicApiAnalyzers、PackageValidation、XML、SourceLink 和包 repository metadata 已进入统一门禁。 |
| EVENTBUS-RC-004 | Passed | EventBus Release 全量 245/245、0 skipped；Generator 专项以及高风险合同测试亦已通过。 |
| EVENTBUS-RC-005 | Passed | 订阅/停止竞争、插件 lease、diagnostics 重入和 channel admission 高风险集合已进行重复并发验证。 |
| EVENTBUS-RC-006 | Passed | Headless fixture 实际选择 6 个 Module、生成并解析 20 个 handler，覆盖多 channel、执行模式和停止路径。 |
| EVENTBUS-RC-007 | Passed | net8.0/net10.0 win-x64 NativeAOT 均完成真实 publish 和进程执行，无 IL2xxx/IL3xxx。 |
| EVENTBUS-RC-008 | Passed | Build/Core/EventBus 被重新打包到本地 NuGet 源；无 `ProjectReference`、独立包缓存的 net8/win-x64 外部消费者成功通过生成式 Module 依赖、Publish/Post、owner 释放及停止合同。 |
| EVENTBUS-RC-009 | Observation passed | ShortRun 12/12 case 产生有效统计；零 case 和无统计结果均返回非零退出码。首份数据只作趋势基线，不承诺跨机器绝对耗时。 |
| EVENTBUS-RC-010 | Passed | Feature 状态漂移已消除，工程门禁与模块文档已经对齐。 |

统一入口 `bash engineering/check-eventbus-release.sh` 已于 2026-09-04 在上述 clean commit 上完整执行通过。它固定使用 Release，覆盖候选范围格式、双 TFM 构建、工程/模块/生成器测试、20 轮压力、Public API、本地 NuGet 外部消费、Benchmark 和候选身份输出。

City 全局 `check-release.sh --configuration Release` 的首次执行在 solution 格式门禁停止，其中包含 Presentation、Data、State 和 Core fixture 等 EventBus 候选范围外文件。没有修改这些其他模块；EventBus 候选范围内格式问题已修正并由专属入口验证。该范围外失败不计作 EventBus 产品失败，也未从 City 全局门禁中删除。

## Feature 证据矩阵

| Feature | 当前状态 | 主要验收证据 | 保留边界 |
| --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Verified | Publish/Post 完成语义、因果链、取消、失败和 immutable result。 | 无模块内阻断项。 |
| AUC-EVENTBUS-002 | EventBus Contract Verified / PluginSystem Integration Pending | owner、LifecycleScope/ApplicationScope、唯一终止事务、quiesce/drain、并发 Stop/Dispose。 | 真实插件 Scope 与卸载集成等待 PluginSystem。 |
| AUC-EVENTBUS-003 | Verified | 稳定 ContractId、精确双向 Registry、Freeze、DI 汇入及生成 catalog 闭包。 | 无模块内阻断项。 |
| AUC-EVENTBUS-004 | Verified | UI/Background/Serialized 调度、错误策略、timeout、cleanup 聚合和后台错误观察。 | 真实 Presentation dispatcher 集成由 Presentation 模块验收。 |
| AUC-EVENTBUS-005 | Verified | 稳定诊断码、metrics、payload 安全投影、限流与 sink 故障/重入隔离。 | 持久化由未来 diagnostics sink/provider 扩展负责。 |
| AUC-EVENTBUS-006 | Verified | EventBusModule、Root DI、Host 三阶段初始化、ApplicationScope、停止与回滚。 | 无模块内阻断项。 |
| AUC-EVENTBUS-007 | Verified | Publish/Post 共享 admission、三种执行模式、五种背压、容量与资源上限。 | 绝对性能数值不是兼容性承诺。 |
| AUC-EVENTBUS-008 | Verified | generator/manifest/registrar、对象图白名单、handler-contract 闭包、双 TFM NativeAOT。 | 无模块内阻断项。 |
| AUC-EVENTBUS-009 | EventBus Contract Verified / PluginSystem Integration Pending | capability、Shared/Private plane、ContributionLease、deadline drain、配额和可回收边界。 | 真实动态插件、manifest、ALC drain/unload 等待 PluginSystem。 |

## 第一轮横向审计收口

| Issue ID | 优先级 | 原问题 | 当前状态 |
| --- | --- | --- | --- |
| EVENTBUS-AUDIT1-001 | P1 | Generated Shared contract 对象图不是封闭白名单。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-002 | P1 | Plugin contribution drain 没有领域内有界等待和稳定 timeout 诊断。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-003 | P1 | Plugin lease 在内部锁内调用总线注册和可重入 diagnostics sink。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-004 | P1 | Generator 不验证 handler 的事件类型是否属于可达 generated/shared catalog。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-005 | P1 | EventBus 1.0 Public API 与包发布门禁不存在。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-006 | P1 | 缺少 5+ Module、20+ handler 的产品级 Headless dogfood。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-007 | P2 | Feature 状态与验收矩阵漂移。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-008 | P2 | Public metrics snapshot 可直接构造非法状态。 | 已修复、已验证。 |
| EVENTBUS-AUDIT1-009 | P2 | Benchmark 覆盖不足且零执行可以假绿。 | 已修复、已验证。 |

## Public API 与包门禁

`engineering/check-public-api.sh` 对 Core 与 EventBus 使用同一套构建产物验证逻辑。2026-09-04 最新执行结果：

| 项目 | Core | EventBus |
| --- | ---: | ---: |
| Shipped API signatures | 352 | 498 |
| XML members（双 TFM 合计） | 622 | 22 |
| SourceLink documents | 2 | 2 |
| NuGet repository URL / HEAD commit | Passed | Passed |
| Release build warnings / errors | 0 / 0 | 0 / 0 |

EventBus XML 当前优先覆盖 `IEventBus`、`IEventPublisher`、`IEventSubscriber` 这组根使用入口。模块的规范性 API 合同仍以 [api-contracts.md](api-contracts.md) 为权威来源；完整 IntelliSense 注释覆盖可以持续增强，但不再允许 XML 产物为空，也不替代 Public API baseline。

## 产品与运行证据

- Headless 产品 fixture：6 个实际选中 Module、20 个 generated handler、多 channel、多执行模式和完整停止路径。
- EventBus Release 全量：245/245、0 skipped。
- EventBus Generator 专项：10/10、0 skipped。
- 生命周期、并发与压力集合：20/20 轮通过，每轮 100/100，合计 2000 次测试实例，无失败、跳过或挂起。
- Headless 动态矩阵：`Publish/Post admission`、owner cancellation、四种错误策略、metrics/diagnostics、Stop-before-Start、并发 Stop 和停止后拒绝发布均由真实 Host 入口断言。
- NativeAOT：net8.0/net10.0 `win-x64` 均完成真实发布和运行，输出稳定成功标识。
- 本地 NuGet 外部消费：重新打包 Build/Core/EventBus 后清空门禁专用缓存；隔离 net8/win-x64 消费者无源码引用完成 restore、self-contained publish 和真实运行，输出 `EVENTBUS_PACKAGE_CONSUMER_OK`。
- Headless 故障注入：异常写入完整 `stderr`，进程退出码为 1，并在有界时间内结束。
- Benchmark：覆盖 diagnostics on/off、0/1/20 subscriber、订阅建立释放、Wait/Reject burst、1/64/256 runtime 和 runtime limit rejection。
- 本机 ShortRun 观察：已有 channel publish 在 1/64/256 channel 下约 `1.34–1.38 us`；0/1/20 subscriber 约 `1.38/2.93/35.48 us`；diagnostics off/on 约 `1.54/3.11 us`；订阅建立释放约 `766.7 ns`。这些是趋势基线，不是跨机器兼容性承诺。
- 测试子进程：统一使用 `stderr + 非零退出码 + 有界等待`；Windows 下只针对测试子进程抑制系统错误对话框，不修改机器级 WER 配置。

## 已知范围限制

- Presentation 的真实 UI dispatcher 集成不属于 EventBus 模块内验收；当前由可控 dispatcher 合同测试覆盖。
- PluginSystem 尚未施工真实插件 manifest、DI Scope、capability 和动态 ALC 集成，因此 Plugin Plane 不能标记为完整 Verified。
- 验收报告自身不进入候选指纹；报告更新不改变已经复验的候选 commit 和内容身份。

## 历史 RC1 说明

2026-09-04 的 Engineering RC1 曾因 Feature 状态漂移、Public API 门禁缺失、Headless 产品组合不足以及 6 个 P1、3 个 P2 审计问题而判定不通过。该结论对当时的候选有效。上述问题随后均已修复并获得专项证据，因此本报告以当前收口状态为主，不再把 RC1 的旧测试数量和未关闭问题作为当前结论。

## 最终判定

EventBus **Application Plane MVP 已达到进入实际应用塑造与后续模块集成阶段的标准，并已形成可复现的正式模块候选**。当前没有已知的 EventBus 模块内 P0/P1 发布阻断项；Plugin Plane 的 Pending 是明确的跨模块施工边界，不影响 Application Plane 候选结论。
