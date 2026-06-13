# AtomUI.City.PluginSystem Update And Rollback 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Update And Rollback` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 更新和回滚设计

适用范围：插件版本切换、运行时更新、Pending 操作、回滚和文件更新约束

### 1. 目标

插件更新必须适配桌面应用长期运行模型。应用可能在运行时下载新插件版本，但旧版本可能正在被路由、ViewModel、后台任务、EventBus handler 或 native 文件引用。

更新设计目标：

- 不原地覆盖插件文件。
- 新旧版本可以并存。
- active 版本由锁定文件明确记录。
- 更新失败可以回滚。
- 运行时无法安全切换时进入 pending。
- `UnloadPending` 不阻塞主应用继续运行。

### 2. active 版本模型

同一 `PluginId` 可以安装多个版本：

```text
installed/
  com.company.sales/
    1.0.0/
    1.1.0/
```

当前启用版本由锁定文件记录：

```json
{
  "plugins": [
    {
      "pluginId": "com.company.sales",
      "activeVersion": "1.1.0",
      "enabled": true
    }
  ]
}
```

规则：

- 不依赖 symlink 表达 active 版本。
- 运行时加载必须根据锁定文件解析 active 版本目录。
- 同一 `PluginId` 同一时间只能有一个 active 版本。
- 已安装但非 active 的版本可以作为回滚目标。

### 3. 更新流程

标准更新流程：

```text
Download new package
-> Install new version side by side
-> Validate new version
-> Prepare switch
-> Deactivate old version
-> Unload old version if runtime switch is required
-> Update lock file activeVersion
-> Activate new version
-> Mark update completed
```

规则：

- 新版本必须先完成安装，再切换 active 版本。
- 旧版本停用失败时不能切换 active 版本。
- 新版本启用失败时必须回滚到旧版本，或保持插件 disabled。
- 锁定文件更新必须记录旧版本和新版本。

### 4. 运行时更新策略

运行时更新按当前插件状态分类：

| 状态 | 策略 |
|---|---|
| 未安装 | 直接安装并可选择启用。 |
| Installed 但未启用 | 安装新版本并切换 active 版本。 |
| Inactive | 可以切换 active 版本。 |
| Active 且可停用 | 停用旧版本后切换。 |
| Active 且无法停用 | 记录 pending update。 |
| UnloadPending | 禁止删除和覆盖旧版本，记录 pending update。 |

运行时更新不能绕过插件生命周期。

### 5. Pending 操作

无法立即完成的更新进入 pending：

```text
plugins/
  pending/
    pending-operations.json
```

建议记录：

- operation id。
- plugin id。
- current version。
- target version。
- package hash。
- requested at。
- reason。
- retry policy。

常见 pending 原因：

- 插件仍有活动路由。
- 插件后台任务未结束。
- 插件加载上下文无法释放。
- native 文件被系统锁定。
- 用户选择下次启动更新。

Host 下次启动时应优先处理 pending 操作，再执行正常插件发现和启用。

### 6. 回滚

回滚目标必须是已安装并验证过的旧版本。

回滚流程：

```text
Deactivate failed version
-> Revoke failed version contributions
-> Update lock file activeVersion to previous version
-> Activate previous version
-> Record rollback result
```

规则：

- 回滚不重新下载旧版本。
- 旧版本目录不能在新版本成功稳定前清理。
- 如果旧版本也无法启用，插件进入 Disabled 或 Faulted。
- 回滚必须保留失败版本诊断。

### 7. 文件更新约束

插件文件不可原地覆盖。

规则：

- 不覆盖 `installed/<plugin-id>/<version>` 内文件。
- 不在插件加载中修改 `root` 目录。
- 不删除 `UnloadPending` 插件目录。
- 不从包缓存直接加载插件程序集。
- native/RID 资产更新必须通过新版本目录完成。
- 清理旧版本前必须确认没有加载上下文、文件句柄或 pending 操作。

这些约束比磁盘空间优化优先级更高。

### 8. 依赖插件更新

如果插件之间存在依赖，更新必须按依赖图处理。

规则：

- 被依赖插件升级前，需要检查依赖方版本范围。
- 依赖方不兼容时，不能只升级被依赖插件。
- 多插件联合更新应作为一个事务计划处理。
- 任一插件切换失败时，已切换插件需要回滚或进入一致的 disabled 状态。

### 9. 设置和状态兼容

插件更新可能改变设置 schema 或持久化状态。

第一版规则：

- 插件 manifest 可以声明 settings schema version。
- 更新前记录旧版本 settings schema。
- 新版本启用失败时不得破坏旧版本配置。
- 自动降级迁移不作为第一版强承诺。
- 复杂迁移应由插件显式提供迁移能力，并绑定插件生命周期。

### 10. 诊断和测试

必须覆盖：

- 未启用插件更新。
- Active 插件运行时更新。
- 停用失败进入 pending。
- `UnloadPending` 阻止删除旧目录。
- 新版本启用失败后回滚。
- 回滚版本也失败。
- 多版本目录并存。
- 锁定文件 active 版本切换。
- pending 操作下次启动恢复。

诊断信息必须包含 PluginId、current version、target version、operation id、切换阶段、pending 原因和回滚结果。
