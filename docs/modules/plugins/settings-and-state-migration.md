# AtomUI.City.PluginSystem Settings And State Migration 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Settings And State Migration` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 设置和状态迁移设计

适用范围：插件配置、用户状态、版本升级、回滚、迁移声明和降级策略

### 1. 目标

插件升级不只替换程序集，还可能改变配置结构、用户状态和缓存格式。迁移设计必须避免新版本启用失败时破坏旧版本可用性。

设计目标：

- 插件配置按 PluginId 隔离。
- 设置 schema 版本可声明。
- 更新前保留旧版本状态。
- 迁移操作可诊断、可回滚或可中止。
- 第一版不承诺复杂自动降级迁移。

### 2. 数据分类

| 类型 | 说明 |
|---|---|
| Plugin configuration | 用户配置、管理员配置、默认配置。 |
| Plugin state | 页面状态、用户偏好、工作区状态。 |
| Plugin cache | 可重建缓存。 |
| Plugin secrets | token、密钥引用，必须交给安全存储。 |
| Plugin diagnostics | 运行诊断和错误记录。 |

安装目录不存放可变数据。

### 3. 配置隔离

配置路径按 PluginId 分区：

```text
plugins/config/<plugin-id>/
plugins/state/<plugin-id>/
plugins/cache/<plugin-id>/
```

规则：

- 插件不能默认写 Host 全局配置。
- 插件只能访问自己的配置 section。
- 访问 Host 配置必须通过授权 contract。
- 卸载插件默认不删除用户配置和状态。

### 4. Schema 声明

插件清单可以声明：

```json
{
  "settings": {
    "schemaVersion": "2.0",
    "defaultConfiguration": "manifests/settings.defaults.json",
    "schema": "manifests/settings.schema.json",
    "migration": {
      "from": "[1.0,2.0)",
      "to": "2.0",
      "mode": "Explicit"
    }
  }
}
```

规则：

- schema 版本随插件配置结构变化。
- 默认配置必须是包内只读资源。
- 用户配置写在用户数据目录。
- 迁移能力必须绑定插件生命周期。

### 5. 更新迁移流程

```text
Install new plugin version
-> Read old settings schema
-> Read new settings schema
-> Determine migration requirement
-> Backup current configuration/state
-> Run migration if required
-> Validate migrated configuration
-> Activate new version
```

规则：

- 迁移在新版本启用前完成。
- 迁移失败不能破坏旧版本配置。
- 新版本启用失败时应保留回滚所需旧状态。
- 缓存数据可以删除重建，不应阻止回滚。

### 6. 回滚策略

第一版回滚策略：

- 可以回滚 active plugin version。
- 可以恢复迁移前备份配置。
- 不承诺自动把新 schema 降级到旧 schema。
- 如果旧版本无法读取迁移后的配置，必须使用备份。
- 如果没有备份，插件进入 Disabled 并提示用户处理。

### 7. 插件卸载

卸载插件时：

- 默认删除 active 记录和安装目录。
- 默认保留配置、状态和用户数据。
- 用户显式选择清理数据时，才删除配置和状态。
- secrets 必须交给 Security 或平台安全存储删除。

### 8. 测试要求

必须覆盖：

- 配置隔离。
- schema 版本升级。
- 迁移成功。
- 迁移失败回滚。
- 新版本启用失败恢复旧配置。
- 卸载保留用户数据。
- 显式清理用户数据。
- 插件不能写 Host 全局配置。
