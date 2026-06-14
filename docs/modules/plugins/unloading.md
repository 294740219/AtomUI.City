# AtomUI.City.PluginSystem Unloading 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Unloading` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

## Public Contract

- 只允许通过 `AtomUI.City.PluginSystem` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-PLUGIN-001 | Plugin Metadata | PluginDeclarationAttributeTests; PluginManifestTests |
| AUC-PLUGIN-002 | Dependency Validation | PluginDependencyTests |
| AUC-PLUGIN-003 | Package Installation | PluginPackageTests |
| AUC-PLUGIN-004 | Discovery | PluginLoadingTests |
| AUC-PLUGIN-005 | Loading | PluginLoadingTests |
| AUC-PLUGIN-006 | MSBuild Contract | PluginMsBuildContractTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## PluginSystem 卸载设计

适用范围：插件停用后卸载、引用释放、卸载重试、UnloadPending 和文件删除约束

### 1. 目标

插件卸载必须保证 Host 不再持有插件程序集、类型、对象、委托、事件订阅、资源或文件句柄。

设计目标：

- 卸载前必须停用插件。
- 所有 Contribution Lease 必须撤销。
- 所有插件 Operation 必须取消。
- 卸载失败进入可诊断的 `UnloadPending`。
- `UnloadPending` 阻止更新和删除文件。
- 当前 runtime 通过 `PluginRuntime.RegisterUnloadLease` 登记可撤销贡献或资源 lease。

### 2. 前置条件

卸载要求插件处于：

- `Inactive`
- `Faulted` 且已完成贡献回滚
- `Loaded` 但未启用

如果插件仍为 `Active`，Host 必须先执行停用流程。

### 3. 卸载流程

```text
Ensure plugin is inactive
-> Mark Unloading
-> Reject new plugin entry
-> Cancel plugin operations
-> Revoke remaining leases
-> Dispose EventBus subscriptions
-> Remove localization and presentation resources
-> Dispose plugin ServiceProvider
-> Clear plugin diagnostics callbacks
-> Request AssemblyLoadContext unload
-> Run cooperative GC verification
-> Mark Unloaded or UnloadPending
```

当前 `PluginRuntime.UnloadAsync` 返回 `PluginUnloadResult`。成功时 state 为 `Unloaded`；任一 lease revoke 失败时 state 为 `UnloadPending`，并返回 `AUCPLG0023` diagnostics。Lease 按反向登记顺序撤销，已撤销 lease 的重复撤销必须幂等。

### 4. 引用释放

卸载前必须释放：

- RouteScope。
- ActivationScope。
- OperationScope。
- EventBus subscription。
- State subscription。
- Timer。
- Dispatcher callback。
- Data connection。
- SignalR connection。
- gRPC streaming call。
- Localization ResourceDictionary。
- Presentation View/ViewModel 映射。
- Plugin ServiceProvider。

任何 registry 接收插件贡献时，都必须能按 PluginId 和 ContributionId 反查并撤销。

### 5. UnloadPending

`UnloadPending` 表示 Host 已请求卸载，但运行时仍无法释放插件加载上下文或相关文件。
当前进入 `UnloadPending` 的直接条件是 runtime lease revoke 失败或仍存在未撤销 lease。

常见原因：

- Host 或其他模块持有插件对象。
- 静态字段保存插件委托。
- EventBus 订阅未解除。
- UI visual tree 仍引用插件 View。
- 后台任务未退出。
- native 文件被锁定。
- 反射对象被长期缓存。

进入 `UnloadPending` 后：

- 插件不能重新启用。
- 插件目录不能删除。
- 插件文件不能覆盖。
- 更新操作进入 pending。
- Host 可以在后续时机重试卸载。

### 6. 卸载重试

重试触发点：

- 路由关闭后。
- 窗口关闭后。
- 后台任务结束后。
- GC 验证后。
- 应用关闭前。
- 下次应用启动前清理。

重试必须保持幂等。已经撤销的 lease 不应重复执行副作用。

### 7. 文件清理

插件文件清理前必须满足：

- 插件状态为 `Unloaded`。
- 没有关联加载上下文。
- 没有关联 pending update。
- 没有 native 文件锁定。
- 锁定文件不再指向该版本。

清理失败不应影响 Host 关闭，但必须记录诊断。

### 8. 诊断

卸载诊断必须能回答：

- 哪个插件无法卸载。
- 卡在哪个阶段。
- 剩余多少 lease。
- 是否还有 Operation。
- 是否还有 EventBus/State subscription。
- 是否还有 UI 引用。
- 是否有 native 文件锁定。

### 9. 测试要求

必须覆盖：

- 正常卸载。
- Active 插件先停用再卸载。
- lease 未撤销导致 `UnloadPending`。
- EventBus 订阅残留。
- UI 引用残留。
- 后台任务未退出。
- native 文件锁定。
- 重试卸载成功。
- `UnloadPending` 阻止更新和删除文件。
