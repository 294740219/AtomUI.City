# AtomUI.City.PluginSystem Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## PluginSystem 诊断和测试设计

适用范围：插件诊断事件、错误码、测试工具、状态机测试和卸载验证

### 1. 目标

插件系统必须能解释插件为什么没有安装、为什么不能加载、为什么不能启用、为什么不能卸载。

设计目标：

- 每个阶段有稳定诊断事件。
- 常见失败有稳定错误码。
- 诊断带 PluginId、Version、operation id 和阶段。
- 测试工具可以驱动插件生命周期。
- 卸载测试能发现引用泄漏。

### 2. 诊断阶段

| 阶段 | 说明 |
|---|---|
| Discover | 扫描插件位置和读取锁定文件。 |
| Install | 下载、hash、解压、staging、安装。 |
| Verify | 清单、兼容性、能力、签名、依赖。 |
| Load | 加载上下文、程序集、模块图、服务容器。 |
| Activate | Contribution 校验和 Lease 创建。 |
| Deactivate | 停止入口、取消 Operation、撤销 Lease。 |
| Unload | 释放资源、卸载加载上下文。 |
| Update | 版本切换、pending、回滚。 |

### 3. 诊断上下文

每条诊断至少包含：

- PluginId。
- PackageId。
- Version。
- PluginProfile。
- operation id。
- phase。
- source path。
- install path。
- contribution id，如果适用。
- exception，如果适用。
- policy result，如果适用。

### 4. 错误码

建议错误码：

| Code | 含义 |
|---|---|
| `AUCPLG0001` | 缺少 PluginId。 |
| `AUCPLG0002` | 多主程序集。 |
| `AUCPLG0003` | PluginId 与安装记录不一致。 |
| `AUCPLG0101` | Host 版本不兼容。 |
| `AUCPLG0102` | 插件 API 版本不兼容。 |
| `AUCPLG0201` | 能力被拒绝。 |
| `AUCPLG0202` | Contribution 超出授权能力。 |
| `AUCPLG0301` | required contribution manifest 缺失。 |
| `AUCPLG0401` | 插件私有类型泄漏到 Host contract。 |
| `AUCPLG0501` | 依赖插件缺失。 |
| `AUCPLG0502` | 依赖版本范围不满足。 |
| `AUCPLG0601` | 包 hash 不匹配。 |
| `AUCPLG0602` | 签名无效。 |
| `AUCPLG0701` | 插件加载失败。 |
| `AUCPLG0801` | 插件卸载进入 UnloadPending。 |
| `AUCPLG0901` | 更新失败并回滚。 |

### 5. 测试工具

Testing 包应提供：

- Plugin test host。
- Fake plugin package builder。
- Fake plugin source。
- Plugin lifecycle driver。
- Fake Host contract registry。
- Fake Contribution registry。
- Load context unload assertion helper。
- Pending update simulator。
- Capability policy test helper。

测试工具不应要求真实 NuGet feed。

### 6. 状态机测试

必须覆盖状态：

```text
Discovered
Verified
Loaded
Initialized
ContributionsApplied
Active
Deactivating
Inactive
Unloading
Unloaded
Invalid
Disabled
Faulted
UnloadPending
```

状态机测试必须断言非法状态转换被拒绝。

### 7. 卸载验证

卸载测试必须验证：

- Lease 全部撤销。
- Operation 全部取消。
- EventBus subscription 全部释放。
- UI 引用全部释放。
- ServiceProvider 已释放。
- AssemblyLoadContext 可以被 GC。
- `UnloadPending` 有明确残留诊断。

### 8. 安装和更新测试

必须覆盖：

- staging 安装成功。
- staging 安装失败清理。
- package cache 损坏。
- hash 不匹配。
- 更新成功。
- 更新进入 pending。
- 回滚成功。
- 回滚失败进入 Disabled。

### 9. 文档完成标准

PluginSystem 的任何行为变更必须同步更新：

- 对应模块设计文档。
- 诊断代码表。
- checklist。
- 测试场景说明。

没有对应文档的行为不应进入实现。
