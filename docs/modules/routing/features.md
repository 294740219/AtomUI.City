# AtomUI.City.Routing Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-ROUTING-001 | Route Definition Syntax | 已实现并通过产品合同测试 | RouteTemplate, RouteDefinitionAttribute, RouteMapAttribute | RouteTemplateTests; RouteDefinitionAttributeTests |
| AUC-ROUTING-002 | Route Graph Build and Snapshot | 已实现并通过产品合同测试 | RouteDescriptor, RouteGraphSnapshot, RouteGraphError | RouteGraphAndMatcherTests |
| AUC-ROUTING-003 | Route Matching and Parameters | 已实现并通过产品合同测试 | RouteMatcher, RouteMatch, RouteParameters | RouteGraphAndMatcherTests; RoutingParameterBoundaryTests |
| AUC-ROUTING-004 | Navigation Transaction | Ready to Start Product Implementation | IRouter, NavigationScope, NavigationResult | NavigationScopeTests |
| AUC-ROUTING-005 | Guard and Redirect Pipeline | Ready to Start Product Implementation | IRouteEnterGuard, IRouteLeaveGuard, RouteGuardResult | RouteGuardTests |
| AUC-ROUTING-006 | ViewModel Target Resolution | Ready to Start Product Implementation | NavigationTarget, ViewModelTargetDescriptor | RouteGraphAndMatcherTests |
| AUC-ROUTING-007 | Plugin Route Contribution | Ready to Start Product Implementation | RouteExtensionPoint, RouteExtensionPointAttribute, RouteGraphSnapshot | RouteGraphAndMatcherTests |
| AUC-ROUTING-008 | Navigation Journal and Reuse | Ready to Start Product Implementation | NavigationSnapshot, NavigationHistoryBehavior, NavigationOptions | NavigationScopeTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Routing 只负责 Route -> ViewModel Target，不创建 View，不提交 VisualTree。 | 必须有实现、测试或工程门禁证据。 |
| RouteGraphSnapshot 发布后不可变，插件撤销只能发布新 snapshot。 | 必须有实现、测试或工程门禁证据。 |
| 导航是事务，失败、取消或 guard 拒绝都不能提交半导航。 | 必须有实现、测试或工程门禁证据。 |
| 所有 route discovery 必须有 AOT 友好的 manifest 或显式注册路径。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-ROUTING-001 Route Definition Syntax

Feature ID: `AUC-ROUTING-001`
Status: 已实现并通过产品合同测试
Goal: 定义接近 ASP.NET routing 的桌面路由模板语法。
Public Contract: RouteTemplate, RouteDefinitionAttribute, RouteMapAttribute
Runtime / Build Behavior: 解析 literal、parameter、optional、catch-all、constraint 和 index/layout/redirect route；RouteTemplate 发布后不可变。
Failure Behavior: 语法错误、重复参数名、非法 catch-all 位置、未知 constraint 必须返回 RouteGraphError 或抛声明异常。
Threading / Cancellation: 解析为纯 CPU 操作；不得访问 UI dispatcher；取消只适用于批量 graph build。
Diagnostics: 模板错误必须带 route text、segment index 和 source type。
Tests: `RouteTemplateTests; RouteDefinitionAttributeTests`
Required Assertions: 断言合法模板、非法模板、参数边界、属性默认值和稳定排序。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-002 Route Graph Build and Snapshot

Feature ID: `AUC-ROUTING-002`
Status: 已实现并通过产品合同测试
Goal: 把模块和插件声明合成为不可变 RouteGraphSnapshot。
Public Contract: RouteDescriptor, RouteGraphSnapshot, RouteGraphError
Runtime / Build Behavior: Graph build 先收集全部 route，再校验冲突、父子关系、layout/index 约束和 extension point，最后一次性发布 snapshot。
Failure Behavior: 冲突、缺失父 route、重复 route id、插件撤销失败不得发布半成品 graph。
Threading / Cancellation: Graph build 可在后台线程执行；发布 snapshot 必须通过原子替换。
Diagnostics: graph build failure 必须包含 route id、owner、conflict target 和 graph version。
Tests: `RouteGraphAndMatcherTests`
Required Assertions: 断言 graph 不可变、冲突拒绝、plugin route revoke 后旧 snapshot 仍只读可用。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-003 Route Matching and Parameters

Feature ID: `AUC-ROUTING-003`
Status: 已实现并通过产品合同测试
Goal: 在不可变 graph 上完成高性能匹配和参数绑定。
Public Contract: RouteMatcher, RouteMatch, RouteParameters
Runtime / Build Behavior: 匹配必须确定性选择最具体 route；参数转换和默认值绑定在 commit 前完成。
Failure Behavior: 参数缺失、格式不匹配、constraint 拒绝返回 Failed match，不能进入 guard。
Threading / Cancellation: 匹配为无副作用读操作，允许并发；取消适用于包含 resolver 的匹配流程。
Diagnostics: 参数绑定失败必须包含 route id、parameter name 和 raw value。
Tests: `RouteGraphAndMatcherTests; RoutingParameterBoundaryTests`
Required Assertions: 断言优先级、参数转换、constraint、并发匹配和非法输入。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-004 Navigation Transaction

Feature ID: `AUC-ROUTING-004`
Status: Ready to Start Product Implementation
Goal: 把一次导航建模为可取消、可诊断、可回滚的事务。
Public Contract: IRouter, NavigationScope, NavigationResult
Runtime / Build Behavior: NavigationScope 从 Created 进入 Matching、Guarding、Resolving、TargetReady，只有全部成功才进入 Committed。
Failure Behavior: 取消、并发策略拒绝、resolver 失败、commit 失败必须保留旧 NavigationSnapshot。
Threading / Cancellation: 异步 guard/resolver 必须观察 token；同一个 Router 的并发导航按 NavigationConcurrencyPolicy 串行、替换或拒绝。
Diagnostics: Navigation failure 必须包含 operation id、source route、target route 和 stage。
Tests: `NavigationScopeTests`
Required Assertions: 断言失败不改变 current snapshot、取消不提交、重复 dispose 幂等、并发策略稳定。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-005 Guard and Redirect Pipeline

Feature ID: `AUC-ROUTING-005`
Status: Ready to Start Product Implementation
Goal: 为进入和离开 route 提供可组合的授权、保存检查和重定向。
Public Contract: IRouteEnterGuard, IRouteLeaveGuard, RouteGuardResult
Runtime / Build Behavior: Guard 按 route hierarchy 和注册顺序执行；redirect 产生新的 NavigationTarget，deny 终止事务。
Failure Behavior: guard 抛异常映射为 NavigationResult Failed；redirect loop 必须检测并停止。
Threading / Cancellation: Guard 可以异步；取消后后续 guard 不得执行。
Diagnostics: guard failure 必须包含 guard type、route id、result status 和 redirect chain。
Tests: `RouteGuardTests`
Required Assertions: 断言 enter/leave 顺序、deny、redirect、loop detection、异常映射和取消。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-006 ViewModel Target Resolution

Feature ID: `AUC-ROUTING-006`
Status: Ready to Start Product Implementation
Goal: Routing 输出 Route -> ViewModel Target，不负责创建 View 或提交 UI。
Public Contract: NavigationTarget, ViewModelTargetDescriptor
Runtime / Build Behavior: RouteDescriptor 必须明确 ViewModel 类型、参数绑定、reuse key 和 activation hint；目标描述符只包含可序列化或可稳定比较的数据。
Failure Behavior: 缺失 ViewModel target、target type 不可构造、参数无法绑定必须在 Presentation 前失败。
Threading / Cancellation: target resolution 不触碰 VisualTree；需要 DI 的 resolver 必须支持取消。
Diagnostics: target resolution failure 必须包含 route id、target type 和 parameter names。
Tests: `RouteGraphAndMatcherTests`
Required Assertions: 断言 target descriptor 内容完整、Routing 不依赖 Presentation、失败不创建 ViewModel。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-007 Plugin Route Contribution

Feature ID: `AUC-ROUTING-007`
Status: Ready to Start Product Implementation
Goal: 支持插件贡献和撤销 route，同时保护 Host route graph。
Public Contract: RouteExtensionPoint, RouteExtensionPointAttribute, RouteGraphSnapshot
Runtime / Build Behavior: 插件 route 必须绑定 plugin owner；load 发布新 snapshot，unload 移除贡献并发布新 snapshot。
Failure Behavior: 插件 route 冲突只拒绝该插件贡献；Host graph 不得被污染。
Threading / Cancellation: 插件 load/unload 与导航并发时，当前导航使用启动时 snapshot，后续导航使用新 snapshot。
Diagnostics: plugin route diagnostics 必须包含 plugin id、contribution id、extension point 和 graph version。
Tests: `RouteGraphAndMatcherTests`
Required Assertions: 断言插件贡献、冲突隔离、卸载撤销、旧 snapshot 只读。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-ROUTING-008 Navigation Journal and Reuse

Feature ID: `AUC-ROUTING-008`
Status: Ready to Start Product Implementation
Goal: 提供桌面应用需要的 back/forward、replace 和 ViewModel reuse 语义。
Public Contract: NavigationSnapshot, NavigationHistoryBehavior, NavigationOptions
Runtime / Build Behavior: Journal 写入必须发生在 commit 后；replace 不增加历史；reuse key 决定是否复用已有 ViewModel target。
Failure Behavior: journal 容量溢出按策略裁剪；失败导航不得写入历史。
Threading / Cancellation: journal 更新跟随 navigation transaction 串行执行。
Diagnostics: journal diagnostics 必须包含 behavior、capacity 和 affected route id。
Tests: `NavigationScopeTests`
Required Assertions: 断言 push/replace/back/forward、容量裁剪、失败不写历史和 reuse key。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
