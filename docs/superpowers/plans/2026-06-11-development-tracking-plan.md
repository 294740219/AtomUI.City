# AtomUI.City 1.0 Release Tracking Plan

本文件是 AtomUI.City 1.0 是否可以发布的唯一进度跟踪文档。仓库中的模块文档、API contract、测试矩阵、设计说明和历史提交只作为实现证据，不作为完成度口径。

## 当前结论

- 1.0 发布状态：不可发布。
- 全局进度：26/118。
- 模块 Feature 合同：26/109。
- 最终发布门禁：0/9。
- 最近校准日期：2026-06-14。

## 统计规则

- 只有本文档中的 `- [x]` 和 `- [ ]` 任务计入全局完成度。
- `- [x]` 表示该任务已有实现、产品合同测试、文档同步和必要门禁证据。
- `- [ ]` 表示该任务尚未达到 1.0 发布完成标准；即使已有部分实现或局部测试，也不能计为完成。
- 任何任务完成时，必须在同一个提交中更新本文档。
- `docs/superpowers/plans/` 目录只保留本文档，不再保留批次计划或局部任务跟踪。
- 模块级进度不再维护独立 `implementation-plan.md`；Feature ID 的发布状态以本文档为准。

## 完成标准

一个 1.0 任务只有同时满足以下条件才能勾选：

- Public API 或内部 contract 已稳定，失败行为、取消、生命周期和线程边界明确。
- 产品合同测试覆盖必要断言和失败路径。
- 相关模块文档、诊断说明、测试矩阵和兼容性说明已同步。
- `dotnet build AtomUICity.slnx`、相关测试、文档检查和 public API 检查在对应提交前通过。
- 如任务影响发布包、模板、CLI、source generator 或平台集成，必须通过对应专项门禁。

## 模块汇总

| 模块 | 完成 | 未完成 | 总数 | 当前状态 |
| --- | ---: | ---: | ---: | --- |
| Core | 7 | 0 | 7 | 已完成 |
| Testing | 9 | 0 | 9 | 已完成 |
| Build | 7 | 0 | 7 | 已完成 |
| Generators | 3 | 5 | 8 | 产品化进行中 |
| Routing | 0 | 8 | 8 | 未完成 |
| Presentation | 0 | 8 | 8 | 未完成 |
| MVVM | 0 | 6 | 6 | 未完成 |
| State | 0 | 8 | 8 | 产品化进行中 |
| EventBus | 0 | 6 | 6 | 产品化进行中 |
| PluginSystem | 0 | 8 | 8 | 未完成 |
| Data | 0 | 9 | 9 | 未完成 |
| Localization | 0 | 7 | 7 | 未完成 |
| Security | 0 | 7 | 7 | 未完成 |
| CLI | 0 | 6 | 6 | 未完成 |
| Templates | 0 | 5 | 5 | 未完成 |
| Release Gates | 0 | 9 | 9 | 未完成 |

## Core

- [x] AUC-CORE-001 Application Host Builder。验收重点：Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。
- [x] AUC-CORE-002 Lifecycle Pipeline。验收重点：stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。
- [x] AUC-CORE-003 Lifecycle Scope Tree。验收重点：leaf-first、parent-child 状态、dispose 后 mutating API 失败。
- [x] AUC-CORE-004 Module Contract。验收重点：依赖排序、默认 id、显式 id、模块来源、PreConfigure 顺序、配置阶段禁止解析运行时服务、配置阶段结束后拒绝继续修改服务注册。
- [x] AUC-CORE-005 DI Registration Markers。验收重点：lifetime、exposed services、AOT metadata 可读。
- [x] AUC-CORE-006 Host Diagnostics。验收重点：现有 AUCHOST001/002/003 和目标失败诊断上下文。
- [x] AUC-CORE-007 UI Dispatcher Contract。验收重点：不可用 dispatcher 返回失败且 Core 不引用 Avalonia。

## Testing

- [x] AUC-TESTING-001 Test Host。验收重点：service、diagnostics、dispose、records。
- [x] AUC-TESTING-002 Fake Dispatcher。验收重点：queue、UI 线程识别、异常、pending count。
- [x] AUC-TESTING-003 Deterministic Scheduler。验收重点：虚拟时间推进、任务顺序、异常记录。
- [x] AUC-TESTING-004 Module Test Host。验收重点：module graph、lifecycle、diagnostics。
- [x] AUC-TESTING-005 Plugin Test Host。验收重点：load/unload、contribution、owner revoke。
- [x] AUC-TESTING-006 Routing Test Host。验收重点：route build、match、navigation helper。
- [x] AUC-TESTING-007 Source Generation Kit。验收重点：generated source snapshot、diagnostics、references。
- [x] AUC-TESTING-008 AOT Check。验收重点：反射扫描、dynamic code、trimming 风险诊断。
- [x] AUC-TESTING-009 Test Layers。验收重点：Unit/Contract/Integration/Platform/Dogfood 分层标记。

## Build

- [x] AUC-BUILD-001 Output Layout。验收重点：artifacts、packages、logs、test-results 都在 output 下。
- [x] AUC-BUILD-002 Package Metadata。验收重点：LGPL v3、repository、symbol、package id 和 dependency group。
- [x] AUC-BUILD-003 Project Inventory。验收重点：src/tests 项目被 inventory 覆盖。
- [x] AUC-BUILD-004 Dependency Boundary。验收重点：runtime 不依赖 Testing、Roslyn 或 test packages。
- [x] AUC-BUILD-005 Source Generator Packaging。验收重点：generator target、analyzer layout、runtime 不引用 generator。
- [x] AUC-BUILD-006 Release Gates。验收重点：docs、format、pack、test gate 可本地执行。
- [x] AUC-BUILD-007 Test Naming。验收重点：测试命名和模块对应关系。

## Generators

- [x] AUC-GENERATORS-001 Incremental Infrastructure。验收重点：incremental 输入隔离、hint name 稳定、无 runtime 依赖。
- [x] AUC-GENERATORS-002 Module Graph。验收重点：DependsOn 图、循环诊断、默认 module id。
- [x] AUC-GENERATORS-003 DI Manifest。验收重点：lifetime、ExposeServices、显式注册和冲突诊断。
- [ ] AUC-GENERATORS-004 Route Manifest。验收重点：route attribute、template、target、排序和诊断。
- [ ] AUC-GENERATORS-005 Plugin Manifest。验收重点：plugin metadata、capability、dependency、contribution。
- [ ] AUC-GENERATORS-006 Localization Manifest。验收重点：culture、resource、fallback、重复 key 诊断。
- [ ] AUC-GENERATORS-007 Presentation View Manifest。验收重点：ViewFor、constructor、registrar source 和诊断。
- [ ] AUC-GENERATORS-008 Diagnostics。验收重点：diagnostic id、severity、message args 和 source location。

## Routing

- [ ] AUC-ROUTING-001 Route Definition Syntax。验收重点：合法模板、非法模板、参数边界、属性默认值和稳定排序。
- [ ] AUC-ROUTING-002 Route Graph Build and Snapshot。验收重点：graph 不可变、冲突拒绝、plugin route revoke 后旧 snapshot 仍只读可用。
- [ ] AUC-ROUTING-003 Route Matching and Parameters。验收重点：优先级、参数转换、constraint、并发匹配和非法输入。
- [ ] AUC-ROUTING-004 Navigation Transaction。验收重点：失败不改变 current snapshot、取消不提交、重复 dispose 幂等、并发策略稳定。
- [ ] AUC-ROUTING-005 Guard and Redirect Pipeline。验收重点：enter/leave 顺序、deny、redirect、loop detection、异常映射和取消。
- [ ] AUC-ROUTING-006 ViewModel Target Resolution。验收重点：target descriptor 内容完整、Routing 不依赖 Presentation、失败不创建 ViewModel。
- [ ] AUC-ROUTING-007 Plugin Route Contribution。验收重点：插件贡献、冲突隔离、卸载撤销、旧 snapshot 只读。
- [ ] AUC-ROUTING-008 Navigation Journal and Reuse。验收重点：push/replace/back/forward、容量裁剪、失败不写历史和 reuse key。

## Presentation

- [ ] AUC-PRESENTATION-001 UI Dispatcher Bridge。验收重点：UI 线程识别、后台 marshal、取消、异常映射和平台不可用。
- [ ] AUC-PRESENTATION-002 View Registry and Locator。验收重点：manifest 注册、显式覆盖、重复拒绝、插件撤销和 O(1) lookup 路径。
- [ ] AUC-PRESENTATION-003 View Factory and Binding。验收重点：构造参数、DataContext、失败回滚、handle dispose 和 lifecycle event。
- [ ] AUC-PRESENTATION-004 Route Outlet Commit。验收重点：成功替换、失败回滚、取消、重复 commit、旧 view dispose 和结果状态。
- [ ] AUC-PRESENTATION-005 Visual Lifecycle Feedback。验收重点：attach/detach、focus、visibility、反馈顺序和 handler 失败隔离。
- [ ] AUC-PRESENTATION-006 Interaction and Validation Bridge。验收重点：handler 注册撤销、无 handler、验证消息变化、控件释放和取消。
- [ ] AUC-PRESENTATION-007 Localization and Resource Bridge。验收重点：culture 切换、fallback、resource revoke、插件资源卸载和局部失败隔离。
- [ ] AUC-PRESENTATION-008 Plugin UI Unload Coordination。验收重点：active view lease、卸载撤销、拒绝卸载、资源释放和重复 unload。

## MVVM

- [ ] AUC-MVVM-001 ViewModel Base and Notification。验收重点：PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。
- [ ] AUC-MVVM-002 Activation and Deactivation。验收重点：状态机、拒绝停用、取消、异常映射和资源释放。
- [ ] AUC-MVVM-003 Command Execution。验收重点：成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。
- [ ] AUC-MVVM-004 Interaction Requests。验收重点：有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。
- [ ] AUC-MVVM-005 Validation Model。验收重点：消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。
- [ ] AUC-MVVM-006 Operation and Cancellation Scope。验收重点：状态转换、取消顺序、重复终态、耗时字段和资源释放。

## State

- [ ] AUC-STATE-001 Writable State。验收重点：原子更新、version、提交后通知、相等值不通知、订阅 dispose、disposed mutation rejection、updater 异常诊断和写拒绝/access policy。
- [ ] AUC-STATE-002 Application State。验收重点：注册、读取、writer、not registered、StateDefinition enum 和 schema version 边界。
- [ ] AUC-STATE-003 Computed State。验收重点：lazy invalidation、依赖失效、缓存、异常诊断、null dependency 拒绝。
- [ ] AUC-STATE-004 State Subscription。验收重点：dispose 后不通知、Background 不阻塞状态提交、Background handler 失败诊断。
- [ ] AUC-STATE-005 State Snapshot。验收重点：不可变、过滤、restore diagnostics、entry version/schema 边界、entries 不含 null。
- [ ] AUC-STATE-006 Collection State。验收重点：change kind、item version、collection version、快照不可变、非法构造参数、dispose 幂等、dispose 后读 API 可用、mutation/restore/subscription API 拒绝。
- [ ] AUC-STATE-007 Diagnostics。验收重点：AUCSTA001-010。
- [ ] AUC-STATE-008 Threading。验收重点：不隐式 UI。

## EventBus

- [ ] AUC-EVENTBUS-001 Typed Publish。验收重点：delivery/post result 边界、null event、预取消 token、disposed bus、publish options 边界、result immutable/null delivery、error policy、diagnostics。
- [ ] AUC-EVENTBUS-002 Subscription Lifecycle。验收重点：dispose 后不再收到事件、StopAsync 移除新发布快照、等待 in-flight handler、owner stop/cancellation 释放、bus dispose 清理 active subscriptions、已 Disposed 后 StopAsync 幂等。
- [ ] AUC-EVENTBUS-003 Contract Registry。验收重点：shared contract assembly match、重复 contract id、稳定默认映射、plugin-private descriptor default id 拒绝、shared registry 拒绝 plugin-private descriptor。
- [ ] AUC-EVENTBUS-004 Dispatch Policy。验收重点：顺序、异常聚合、停止策略、未知 error policy 拒绝。
- [ ] AUC-EVENTBUS-005 Diagnostics。验收重点：EventBus.Event* 现有代码、failure/cancellation 诊断包含 contract id、event id 和 subscription id。
- [ ] AUC-EVENTBUS-006 DI Registration。验收重点：默认服务、可替换 diagnostics 和 provider dispose 释放 EventBus singleton。

## PluginSystem

- [ ] AUC-PLUGIN-001 Plugin Metadata。验收重点：id、version、mainAssembly、schema 和 required fields。
- [ ] AUC-PLUGIN-002 Dependency Validation。验收重点：missing、cycle、version mismatch diagnostics。
- [ ] AUC-PLUGIN-003 Package Installation。验收重点：staging cleanup、installed record、path normalization。
- [ ] AUC-PLUGIN-004 Discovery。验收重点：invalid install record diagnostics 且继续扫描其他插件。
- [ ] AUC-PLUGIN-005 Loading。验收重点：Loaded/Failed 状态和 diagnostics。
- [ ] AUC-PLUGIN-006 MSBuild Contract。验收重点：MSBuild property、output path、package content。
- [ ] AUC-PLUGIN-007 Diagnostics。验收重点：AUCPLG0000-0021 关键路径。
- [ ] AUC-PLUGIN-008 Unload Contract。验收重点：Disable -> Unloading -> Unloaded/UnloadPending。

## Data

- [ ] AUC-DATA-001 Request Pipeline。验收重点：执行顺序、取消不写缓存、retry diagnostics。
- [ ] AUC-DATA-002 HTTP Transport。验收重点：status -> DataErrorKind 映射。
- [ ] AUC-DATA-003 gRPC Transport。验收重点：GrpcStatusCode 映射。
- [ ] AUC-DATA-004 SignalR Transport。验收重点：invocation context。
- [ ] AUC-DATA-005 Connection Lifecycle。验收重点：状态转换、owner 释放。
- [ ] AUC-DATA-006 Authentication。验收重点：credential before transport。
- [ ] AUC-DATA-007 Caching。验收重点：key 组成和 hit/miss。
- [ ] AUC-DATA-008 Error Model。验收重点：result 不混用 success/error。
- [ ] AUC-DATA-009 DI Registration。验收重点：默认服务。

## Localization

- [ ] AUC-LOCALIZATION-001 Culture State and Fallback。验收重点：默认 culture、fallback 顺序、非法 culture 和重复切换。
- [ ] AUC-LOCALIZATION-002 Language Package Provider。验收重点：provider 注册、重复拒绝、取消、格式错误和 owner revoke。
- [ ] AUC-LOCALIZATION-003 Lazy Package Loading。验收重点：按需加载、并发合并、失败 fallback、不同 culture 独立缓存。
- [ ] AUC-LOCALIZATION-004 Lookup and Missing Key Fallback。验收重点：scope lookup、fallback、缺失 key、参数格式化和订阅更新。
- [ ] AUC-LOCALIZATION-005 Assembly Language Packages。验收重点：独立 assembly、属性声明、资源读取、缺失资源和 unload owner。
- [ ] AUC-LOCALIZATION-006 Presentation Refresh Bridge。验收重点：bridge 调用、局部失败、批量刷新和不依赖 Avalonia 类型。
- [ ] AUC-LOCALIZATION-007 Plugin Package Revocation。验收重点：撤销后不可 lookup、旧 snapshot 稳定、订阅释放和重复 revoke。

## Security

- [ ] AUC-SECURITY-001 Authentication State Store。验收重点：snapshot 不可变、状态切换、订阅通知、重复设置和 logout。
- [ ] AUC-SECURITY-002 Current Principal Access。验收重点：authenticated、anonymous、claims 读取和并发 snapshot。
- [ ] AUC-SECURITY-003 Permission Registry and Checker。验收重点：注册、重复、未注册、插件撤销和 checker result。
- [ ] AUC-SECURITY-004 Authorization Policy Evaluation。验收重点：成功、拒绝、失败、取消、多 requirement 和 provider 异常。
- [ ] AUC-SECURITY-005 Route Authorization Guard。验收重点：allow、deny、redirect login、取消和 Routing 无 Security 反向依赖。
- [ ] AUC-SECURITY-006 Command Authorization。验收重点：状态变化、禁用/隐藏策略、订阅释放和权限撤销。
- [ ] AUC-SECURITY-007 Access Token Provider。验收重点：成功、失败、不可用、取消、DI 默认 provider 和 Data 集成前置条件。

## CLI

- [ ] AUC-CLI-001 Command Model。验收重点：入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。
- [ ] AUC-CLI-002 New App Command。验收重点：生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。
- [ ] AUC-CLI-003 Build and Test Commands。验收重点：成功、失败、非零 exit code、取消、CI 模式和输出截断。
- [ ] AUC-CLI-004 Plugin Inspect and Doctor。验收重点：合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。
- [ ] AUC-CLI-005 AI-Friendly Envelope。验收重点：schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。
- [ ] AUC-CLI-006 Non-Interactive and CI Mode。验收重点：CI、non-interactive、stdin unavailable、需要确认时失败。

## Templates

- [ ] AUC-TEMPLATES-001 Application Template。验收重点：生成、restore/build/test、命名空间、包引用、无绝对路径。
- [ ] AUC-TEMPLATES-002 Package Layout。验收重点：required files、路径规范化、重复文件、路径逃逸和 package id。
- [ ] AUC-TEMPLATES-003 Template Variables。验收重点：变量默认值、非法值、命名空间生成和错误消息。
- [ ] AUC-TEMPLATES-004 Plugin Template。验收重点：单 assembly、NuGet metadata、manifest、msbuild 属性和测试项目。
- [ ] AUC-TEMPLATES-005 Test Template。验收重点：测试项目 build/test、TestLayer、Testing 引用边界和命名规则。

## Release Gates

- [ ] AUC-RELEASE-001 Full solution build。通过 `dotnet build AtomUICity.slnx`，且无 warning、无 error。
- [ ] AUC-RELEASE-002 Full solution tests。通过 `dotnet test AtomUICity.slnx --no-build`。
- [ ] AUC-RELEASE-003 Documentation gate。通过 `bash engineering/check-docs.sh`。
- [ ] AUC-RELEASE-004 Public API gate。通过 `bash engineering/check-public-api.sh`，并确认 public API 变化已审阅。
- [ ] AUC-RELEASE-005 Package generation。通过 `bash engineering/pack.sh --configuration Release`。
- [ ] AUC-RELEASE-006 Package validation。通过 `bash engineering/validate-packages.sh --configuration Release`。
- [ ] AUC-RELEASE-007 Template smoke gate。通过 `bash engineering/check-template-smoke.sh`。
- [ ] AUC-RELEASE-008 CI-equivalent local gate。通过 `bash engineering/test-ci.sh` 和必要的 platform integration gate。
- [ ] AUC-RELEASE-009 Release notes and versioning。通过 `bash engineering/generate-release-notes.sh`，并完成 1.0 版本号、包元数据和发布说明审阅。

## 后续维护规则

- 新增、拆分或删除 1.0 任务时，必须同步更新当前结论、模块汇总和对应任务段落。
- 不允许通过模块局部文档覆盖本文档中的完成状态。
- 不允许新增其他任务跟踪文件；临时执行计划完成后必须折叠回本文档或删除。
- 每次工作对齐时，按本文档输出 `已完成数/总数`。
