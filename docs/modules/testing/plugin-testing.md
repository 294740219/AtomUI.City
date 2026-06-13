# AtomUI.City.Testing Plugin Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Testing` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-TESTING-001 | Test Host | TestHostTests |
| AUC-TESTING-002 | Fake Dispatcher | FakeUiDispatcherTests |
| AUC-TESTING-003 | Deterministic Scheduler | SharedTestUtilitiesTests |
| AUC-TESTING-004 | Module Test Host | ModuleTestHostTests |
| AUC-TESTING-005 | Plugin Test Host | PluginTestHostTests |
| AUC-TESTING-006 | Routing Test Host | RoutingTestHostTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `生产运行时反向依赖 AtomUI.City.Testing` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## PluginSystem 测试设计

适用范围：插件包、发现、安装、加载、启用、停用、卸载、更新、回滚、UnloadPending 和安全策略

### 1. 目标

PluginSystem 测试必须证明插件运行时扩展可安装、可加载、可撤销、可卸载、可回滚，并且不会污染 Host。

### 2. PluginTestHost

Testing 提供：

- fake plugin package builder。
- fake plugin source。
- fake package cache。
- fake installed directory。
- fake lock file。
- fake plugin manifest。
- fake contribution manifest。
- plugin lifecycle driver。
- unload assertion helper。
- trust policy fake。

### 3. 包和安装测试

必须覆盖：

- 标准包安装。
- 本地包安装。
- staging。
- package hash。
- content hash。
- manifest validation。
- required contribution manifest。
- install record。
- lock file。
- 安装失败恢复。

### 4. 生命周期测试

必须覆盖：

- discover。
- verify。
- load。
- activate。
- deactivate。
- unload。
- disabled。
- faulted。
- invalid。
- UnloadPending。

状态机必须拒绝非法转换。

### 5. 卸载测试

必须断言：

- 新入口被阻止。
- active route 被关闭。
- Operation 被取消。
- EventBus subscription 被释放。
- State subscription 被释放。
- Data connection 被停止。
- Localization resource 被撤销。
- Presentation resource 被撤销。
- Contribution Lease 全部撤销。
- ServiceProvider 被释放。
- AssemblyLoadContext 可释放。

### 6. 更新和回滚测试

必须覆盖：

- side-by-side version install。
- active version switch。
- 更新成功。
- 更新失败回滚。
- rollback failure。
- pending update。
- UnloadPending 阻止删除和覆盖文件。

### 7. 安全测试

必须覆盖：

- unknown source。
- hash mismatch。
- invalid signature。
- capability denied。
- unauthorized contribution。
- private contract leakage。

### 8. 测试隔离

插件测试必须使用测试临时目录，不得使用真实用户插件目录。

插件测试不要求真实 NuGet feed。
