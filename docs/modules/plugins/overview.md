# AtomUI.City.PluginSystem

文档等级：Level 3
成熟度：Partially Implemented
执行边界：Host runtime plugin boundary
程序集：`AtomUI.City.PluginSystem`
源码：`src/AtomUI.City.PluginSystem`
测试：`tests/AtomUI.City.PluginSystem.Tests`

## 模块定位

运行时插件发现、安装记录、manifest、依赖校验、加载、启停和可卸载贡献管理。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- 插件包安装必须先进入 staging，校验成功后原子切换到 installed。
- 插件运行时入口默认一个主 assembly。
- 插件贡献必须有 lease，卸载先 revoke contribution 再释放插件对象。
- 跨插件边界类型必须位于 Host 共享 contract 程序集。
- 插件卸载失败不能破坏 Host，必须进入 UnloadPending 或失败结果。
- 安装路径必须防止路径穿越。

## 模块目标

- 让插件以独立 NuGet 包发布，默认一个主 assembly。
- 让 Host 可以在运行时发现、安装、加载、启用、停用和卸载插件。
- 让插件贡献可索引、可撤销、可诊断、可兼容性检查。
- 用 MSBuild 属性和 manifest 生成简化插件开发和打包。

## 明确非目标

- 不让插件修改 Host root provider 的不可撤销单例。
- 不在插件系统内实现路由、视图、状态、权限等具体业务能力。
- 不绕过 Host 生命周期执行插件代码。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

AtomUI.City.PluginSystem 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 与 PluginSystem 的关系

PluginSystem 是插件边界所有者，插件通过 manifest、Module 和 Contribution 暴露能力。

## 与 Testing 的关系

`tests/AtomUI.City.PluginSystem.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

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
| [implementation-plan.md](implementation-plan.md) | Feature 到现有基线、缺口、必补测试和实现工作的追踪。 |

## 当前成熟度状态

Partially Implemented

该状态表示模块已有实现基线，但还需要按产品级合同补齐实现和测试。单个功能点状态以 [implementation-plan.md](implementation-plan.md) 为准。
