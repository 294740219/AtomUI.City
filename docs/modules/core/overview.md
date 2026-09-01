# AtomUI.City.Core

文档等级：Level 3
成熟度：Partially Implemented
执行边界：Host runtime kernel
程序集：`AtomUI.City.Core`
源码：`src/AtomUI.City.Core`
测试：`tests/AtomUI.City.Core.Tests`

## 模块定位

应用宿主、生命周期、中间件、模块系统、DI 约定、配置入口、诊断和 UI 调度抽象的最小内核。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 后必须冻结服务注册入口。
- LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。
- StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则。
- 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。

## 模块目标

- 提供应用运行时唯一根对象和构建入口。
- 用显式生命周期管线约束启动、运行、停止和释放。
- 让模块通过一致的配置、服务注册、初始化和关闭阶段接入 Host。
- 保持 UI 无关、AOT 友好、可测试。

## 明确非目标

- 不实现 Avalonia/AtomUI 控件、主题或窗口。
- 不负责插件包安装和动态程序集加载。
- 不实现状态、路由、数据访问、权限或本地化具体能力。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

Core 定义 Host 本体，所有运行时模块都通过 IApplicationHostBuilder、LifecyclePipeline 和 ModuleBase 接入。

## 命名空间

Core 的 public API 必须使用与程序集一致的 `AtomUI.City.Core.*` 根命名空间：

- `AtomUI.City.Core.Hosting`
- `AtomUI.City.Core.Lifecycle`
- `AtomUI.City.Core.Modularity`
- `AtomUI.City.Core.DependencyInjection`
- `AtomUI.City.Core.Diagnostics`
- `AtomUI.City.Core.Threading`

不得重新引入省略 `Core` 层级的扁平命名空间。

## 与 PluginSystem 的关系

Core 不加载插件，也不创建插件独立 ServiceProvider 或通用 ContributionLease。Core 只提供插件模块可复用的 Module、Lifecycle 和 Diagnostics 基础合同，并保证应用 Root ServiceProvider 在 Build 后不可修改；插件发现、隔离服务容器、贡献撤销和卸载编排由 PluginSystem 负责。

## 与 Testing 的关系

`tests/AtomUI.City.Core.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| [architecture.md](architecture.md) | 核心不变量、对象模型、状态机、流程、失败矩阵、性能边界。 |
| [features.md](features.md) | Feature ID、实现合同、public contract、失败行为和验收标准。 |
| [api-contracts.md](api-contracts.md) | API family、关键方法、参数/返回/异常/取消/并发/Dispose 后行为。 |
| [lifecycle.md](lifecycle.md) | 模块特有生命周期、Host shutdown、插件动态变更和失败处理。 |
| [threading.md](threading.md) | 线程边界、UI dispatcher、后台任务、并发冲突和死锁规避。 |
| [diagnostics.md](diagnostics.md) | 现有诊断码、产品级目标诊断、上下文字段和测试断言。 |
| [testing.md](testing.md) | 具体测试矩阵、必须断言的行为、测试类型和缺口处理。 |
| [compatibility.md](compatibility.md) | public API、配置、manifest、snapshot、generated output、CLI envelope 和包布局兼容。 |
| [integration.md](integration.md) | 跨模块依赖方向、生命周期、线程和失败行为。 |
| [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) | Feature 到现有基线、缺口、必补测试和实现工作的追踪。 |

## 当前成熟度状态

Partially Implemented

`AUC-CORE-001` 到 `AUC-CORE-008` 已分别达到 `Verified`，包括 Host、Lifecycle、Scope、Module、DI markers、Host diagnostics、UI dispatcher contract 和 Generated Module Catalog。Generator 生成强类型 registrar，Core 通过 `GeneratedModuleManifestAttribute` 和 `ModuleCatalog` 在 Build 阶段消费，不再把未定义的 `ModuleManifest` 数据类型作为当前合同。

模块整体仍为 `Partially Implemented`：自定义桌面 `IApplicationLifetime`、跨模块通用 Contribution/ContributionLease、ErrorPolicy/业务诊断管线以及插件独立 ServiceProvider 编排均未分配 Core Feature ID，也不属于当前 Core 1.0 合同。详细设计中保留的相关内容是后续路线图，不能用 Core Feature 的 `Verified` 状态推导其已经实现。
