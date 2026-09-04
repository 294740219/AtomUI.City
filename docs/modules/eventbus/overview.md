# AtomUI.City.EventBus

文档等级：Level 3
成熟度：In Design
执行边界：Host runtime service
程序集：`AtomUI.City.EventBus`
源码：`src/AtomUI.City.EventBus`
测试：`tests/AtomUI.City.EventBus.Tests`

## 模块定位

模块、插件和运行时服务之间的类型化进程内事件总线。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- Publish 不隐式切 UI 线程。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。
- 跨插件事件类型必须来自 Host 共享 contract 程序集。
- 默认派发顺序稳定。

## 模块目标

- 降低模块直接引用。
- 提供可释放订阅、确定性派发、错误策略和诊断。
- 约束跨插件事件 contract 必须来自 Host 共享程序集。

## 明确非目标

- 不实现跨进程消息队列。
- 不保证事件持久化。
- 不作为状态存储。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

AtomUI.City.EventBus 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 与 PluginSystem 的关系

`AUC-EVENTBUS-009` 先在 EventBus 中冻结 Shared/Private Plane、capability、领域 lease 和撤销协议；真实动态插件集成、AssemblyLoadContext drain/unload 和最终验收等待 PluginSystem 施工。Plugin Plane 不阻断 `AUC-EVENTBUS-001` 至 `008` 组成的 Application Plane MVP。

## 与 Testing 的关系

`tests/AtomUI.City.EventBus.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| [architecture.md](architecture.md) | 核心不变量、对象模型、状态机、流程、失败矩阵、性能边界。 |
| [features.md](features.md) | Feature ID、实现合同、public contract、失败行为和验收标准。 |
| [api-contracts.md](api-contracts.md) | API family、关键方法、参数/返回/异常/取消/并发/Dispose 后行为。 |
| [lifecycle.md](lifecycle.md) | 模块特有生命周期、Host shutdown、插件动态变更和失败处理。 |
| [threading.md](threading.md) | 线程边界、UI dispatcher、后台任务、并发冲突和死锁规避。 |
| [diagnostics.md](diagnostics.md) | EventBus 1.0 诊断目录、上下文字段和测试断言。 |
| [testing.md](testing.md) | 具体测试矩阵、必须断言的行为、测试类型和缺口处理。 |
| [compatibility.md](compatibility.md) | public API、配置、manifest、snapshot、generated output、CLI envelope 和包布局兼容。 |
| [integration.md](integration.md) | 跨模块依赖方向、生命周期、线程和失败行为。 |
| [release-candidate-validation.md](release-candidate-validation.md) | EventBus Application Plane MVP 发布候选的冻结规则、验收门禁、命令和判定标准。 |
| [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) | 仓库级进度镜像；必须以下述模块 Feature 状态为准，不能反向覆盖模块设计状态。 |

## 当前成熟度状态

`In Design`

EventBus 以 [features.md](features.md) 中的九个 Feature 为唯一状态来源。当前所有 Feature 均处于设计收口阶段，既有源码和测试不作为设计完成证明。`001–008 Verified` 表示 Application Plane MVP 完成；`009 Verified` 表示 Plugin Plane 完成。
