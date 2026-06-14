# AtomUI.City.PluginSystem Discovery 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Discovery` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 发现设计

适用范围：插件目录、插件包扫描、来源优先级、禁用策略和发现诊断

### 1. 目标

插件发现负责回答两个问题：

- Host 可以从哪些位置发现插件。
- 哪些插件版本应进入后续验证、加载和启用流程。

发现阶段不能执行插件代码，不能加载插件程序集，不能创建插件服务。

### 2. 插件位置类型

Host 可以扫描四类插件位置：

| 类型 | 用途 | 默认是否启用 |
|---|---|---:|
| Bundled plugins | 随应用发布的内置插件，位于应用安装目录。 | 是 |
| Machine plugins | 管理员部署的机器级插件。 | 由 Host policy 决定 |
| User plugins | 框架下载、用户安装、从磁盘安装的插件。 | 是 |
| Dev plugins | 开发模式下显式指定的本地插件路径。 | 仅 Development |

规则：

- 框架下载的插件默认进入 User plugins。
- Bundled plugins 应视为只读，不允许用户从插件管理器删除文件。
- Dev plugins 只允许在 Development 或显式开启的诊断模式中使用。
- Machine plugins 需要受系统权限和 Host policy 管理。

### 3. 默认目录

插件安装目录使用用户级应用数据目录。

| 平台 | User plugins |
|---|---|
| Windows | `%APPDATA%\AtomUI.City\Apps\<AppId>\<PluginProfile>\plugins` |
| macOS | `~/Library/Application Support/AtomUI.City/Apps/<AppId>/<PluginProfile>/plugins` |
| Linux | `${XDG_DATA_HOME:-~/.local/share}/AtomUI.City/Apps/<AppId>/<PluginProfile>/plugins` |

插件包缓存使用用户级缓存目录。

| 平台 | Package cache |
|---|---|
| Windows | `%LOCALAPPDATA%\AtomUI.City\Apps\<AppId>\<PluginProfile>\plugin-cache` |
| macOS | `~/Library/Caches/AtomUI.City/Apps/<AppId>/<PluginProfile>/plugin-cache` |
| Linux | `${XDG_CACHE_HOME:-~/.cache}/AtomUI.City/Apps/<AppId>/<PluginProfile>/plugin-cache` |

目录中的 `<PluginProfile>` 由 Host 插件 API 兼容版本和渠道组成，例如 `1.0-stable`。

### 4. 目录结构

User plugins 推荐结构：

```text
plugins/
  installed/
    <plugin-id>/
      <version>/
        root/
          lib/net10.0/<PluginAssembly>.dll
          atomui-city/plugin.json
          atomui-city/manifests/*.json
          atomui-city/locales/...
        install.json
  staging/
    <operation-id>/
  pending/
    pending-operations.json
  atomui-city.plugins.lock.json
```

包缓存推荐结构：

```text
plugin-cache/
  packages/
    <package-id>/
      <version>/
        <sha256>.nupkg
```

规则：

- `installed` 只保存已验证并完成安装的插件版本。
- `staging` 只保存进行中的安装或更新。
- `pending` 保存因运行中插件无法替换而延迟的操作。
- `atomui-city.plugins.lock.json` 是当前启用状态的恢复依据。
- `plugin-cache` 只保存下载包，不作为运行时加载目录。

### 5. 发现流程

发现流程：

```text
Resolve plugin locations
-> Read lock file
-> Scan installed plugin directories
-> Read plugin manifest
-> Read install record
-> Build plugin candidates
-> Apply disabled list
-> Resolve active versions
-> Produce discovery result
```

规则：

- 发现阶段只读取 `plugin.json`、`install.json` 和锁定文件。
- 发现阶段不解析插件依赖程序集。
- 发现阶段不创建 `AssemblyLoadContext`。
- 发现结果必须保留插件来源位置。
- 同一个 `PluginId` 的多版本候选由锁定文件和 Host policy 决定 active 版本。
- `installed/` 下的非目录插件条目和插件目录下的非目录版本条目必须报告诊断并继续扫描其他插件。

### 6. 来源优先级

默认优先级建议：

```text
Dev plugins
-> User plugins
-> Machine plugins
-> Bundled plugins
```

这只是候选选择优先级，不代表安全信任级别。

规则：

- Dev plugins 只在开发模式下参与。
- User plugins 可以覆盖 Bundled plugins，但必须通过 Host policy。
- Machine plugins 是否可以覆盖 User plugins 由应用策略决定。
- 同一来源内同一 `PluginId` 多版本只能选择一个 active 版本。

如果启用来源覆盖，诊断中必须记录被覆盖插件版本和覆盖原因。

### 7. 禁用策略

禁用状态应记录在锁定文件或用户配置中，不通过删除目录表达。

禁用类型：

| 类型 | 说明 |
|---|---|
| UserDisabled | 用户手动禁用。 |
| PolicyDisabled | Host policy 禁用。 |
| CompatibilityDisabled | 版本或 contract 不兼容。 |
| FaultDisabled | 上次加载或启用失败后禁用。 |
| SecurityDisabled | 签名、来源或能力校验失败。 |

禁用规则：

- 禁用插件可以保留安装目录。
- 禁用插件不进入加载阶段。
- 用户禁用不应删除插件配置和状态。
- 安全禁用必须要求重新验证后才能启用。

### 8. 配置覆盖

Host 应允许应用显式配置插件根目录。

建议配置项：

```json
{
  "AtomUI": {
    "City": {
      "Plugins": {
        "UserPluginsPath": "",
        "PackageCachePath": "",
        "EnableDevPlugins": false,
        "DevPluginPaths": []
      }
    }
  }
}
```

规则：

- 默认路径必须跨平台可用。
- 自定义路径必须在启动期冻结。
- 路径变更不应在运行时自动迁移插件。
- 自定义路径必须进入诊断输出。

### 9. 发现错误策略

发现错误默认不阻止应用启动。

| 错误 | 策略 |
|---|---|
| 插件根目录不存在 | 创建目录或跳过。 |
| 插件目录不可读 | 跳过并记录诊断。 |
| 安装目录结构非法 | 跳过该条目并记录诊断。 |
| 清单缺失 | 标记 Invalid。 |
| 清单 JSON 无效 | 标记 Invalid。 |
| 安装记录缺失 | 标记 Invalid 或要求修复。 |
| 锁定文件损坏 | 使用恢复策略并记录严重诊断。 |
| 同一插件多版本冲突 | 按锁定文件选择，无法选择则禁用。 |

### 10. 诊断和测试

必须覆盖：

- 默认目录解析。
- 自定义目录解析。
- 多来源扫描。
- 同一 `PluginId` 多版本发现。
- 禁用列表生效。
- 锁定文件损坏恢复。
- 清单缺失。
- 清单无效。
- 发现阶段不加载程序集。

诊断信息必须包含扫描目录、插件来源、PluginId、Version、禁用原因和候选选择结果。
