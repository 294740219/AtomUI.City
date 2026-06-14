# AtomUI.City.PluginSystem Dependency Resolution 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Dependency Resolution` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 依赖解析设计

适用范围：插件依赖、程序集依赖、共享 contract、私有依赖、native/RID 资产和加载上下文解析

### 1. 目标

依赖解析必须保证插件可以隔离加载、可诊断卸载，并且不会污染 Host 默认加载上下文。

设计目标：

- Host contract 由 Host 默认加载上下文提供。
- 插件私有依赖由插件加载上下文解析。
- 插件之间通过 `PluginId` 和版本范围表达依赖。
- 依赖解析结果可记录、可复现。
- 不通过运行时随机探测加载程序集。

### 2. 依赖类型

| 类型 | 说明 |
|---|---|
| Host contract | Host 显式暴露给插件的共享 contract 程序集。 |
| Plugin dependency | 插件依赖另一个插件的能力或 contract。 |
| Private assembly dependency | 插件私有实现依赖。 |
| Framework dependency | .NET shared framework 或应用已提供的框架依赖。 |
| Native/RID dependency | 插件携带的 native 库。 |
| Resource dependency | 本地化资源、图片、样式和 `.locpack`。 |

### 3. Host Contract 解析

Host contract 必须从 Host 默认加载上下文解析。

规则：

- 插件只引用 Host 提供的 contract 程序集。
- 插件包不应携带 Host contract 私有副本。
- Host contract 版本必须在清单中声明。
- 跨插件边界的 DTO、事件、消息 key、服务 contract 必须位于 Host 共享 contract 程序集。
- Host 不长期持有插件私有类型实例。

如果插件携带了 Host contract 私有副本，加载前应拒绝或忽略该副本，并记录诊断。

### 4. 插件私有依赖

插件私有依赖从插件 `root` 目录解析。

推荐解析依据：

- 插件 `.deps.json`。
- `atomui-city/plugin.json` 依赖摘要。
- `runtimes/<rid>` native 资产目录。
- 插件安装记录中的 content hash。

规则：

- 私有依赖只在当前插件加载上下文内可见。
- 私有依赖不能自动暴露给 Host 或其他插件。
- 相同依赖不同版本可以由不同插件分别加载。
- 解析失败时插件加载失败，不影响 Host 启动。

### 5. 插件间依赖

插件依赖通过 `PluginId` 和版本范围声明：

```json
{
  "dependencies": {
    "plugins": [
      {
        "pluginId": "com.company.identity",
        "versionRange": "[1.0.0,2.0.0)"
      }
    ]
  }
}
```

规则：

- 插件间依赖不等于程序集引用透传。
- 依赖插件之间应通过共享 contract 或 Host registry 交互。
- 被依赖插件必须先验证和启用。
- 依赖图存在环时，默认拒绝相关插件启用。
- 依赖图存在环时，环中的每个 plugin id 必须产生 `PluginDependencyCycle` 诊断。
- 依赖插件停用时，依赖方必须先停用或降级能力。

### 6. 加载上下文

每个可卸载插件使用独立加载上下文。

```text
Host Default Context
  -> Host contracts
Plugin Load Context
  -> Plugin main assembly
  -> Plugin private dependencies
  -> Plugin resources
```

规则：

- 插件主程序集和私有依赖由插件加载上下文加载。
- Host contract 从默认上下文返回。
- 不把插件私有依赖加载进默认上下文。
- 加载上下文创建、解析失败和卸载结果必须记录诊断。

### 7. Native/RID 资产

native 资产必须显式声明。

规则：

- 只从已验证的安装目录加载 native 资产。
- native 资产参与 content hash。
- RID 选择必须在加载前完成。
- native 文件锁定会影响更新和删除，失败时进入 pending 或 `UnloadPending`。

### 8. 解析结果锁定

依赖解析结果应写入锁定文件或诊断快照。

记录内容：

- 插件版本。
- 依赖插件版本。
- Host contract 版本。
- 私有依赖路径。
- native/RID 选择结果。
- 解析失败原因。

Host 下次启动可以使用记录辅助诊断，但仍应重新验证实际文件和版本。

### 9. 测试要求

必须覆盖：

- Host contract 从默认上下文加载。
- 插件私有依赖版本隔离。
- 插件携带 Host contract 副本被拒绝。
- 插件依赖缺失。
- 插件依赖版本不满足。
- 依赖环检测。
- native/RID 资产解析失败。
- 插件卸载后加载上下文可释放。
