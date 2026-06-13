# AtomUI.City.PluginSystem Contribution Index 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Contribution Index` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 贡献索引设计

适用范围：插件贡献清单索引、贡献清单文件、必填策略和构建期生成

### 1. 目标

贡献索引用于把插件清单和各模块贡献清单连接起来。它让 Host 在不执行插件代码的情况下知道插件准备贡献哪些能力。

设计目标：

- 贡献清单按模块拆分。
- Host 可以加载前校验必填贡献是否存在。
- 各模块只解析自己负责的贡献清单。
- Contribution 可以追踪到 Plugin、Module 和源文件。
- 支持 source generator 和 MSBuild 稳定生成。

### 2. 清单索引

`plugin.json` 中的 `contributions` 字段只保存索引：

```json
{
  "contributions": {
    "modules": {
      "path": "manifests/modules.json",
      "required": true
    },
    "routes": {
      "path": "manifests/routes.json",
      "required": false
    },
    "localization": {
      "path": "manifests/localization.json",
      "required": false
    }
  }
}
```

规则：

- `path` 必须是插件 `atomui-city` 目录内相对路径。
- `required` 为 true 且文件缺失时，插件验证失败。
- 未声明的贡献类型默认不存在。
- Host 不应为了发现贡献而执行插件代码。

### 3. 推荐贡献清单

| 清单 | 所属模块 | 内容 |
|---|---|---|
| `modules.json` | Core ModuleSystem | 插件模块和模块依赖。 |
| `routes.json` | Routing | 路由、导航元数据、ViewModel target。 |
| `permissions.json` | Security | 权限点、策略元数据。 |
| `presentation.json` | Presentation | View 映射、资源、菜单、工具栏入口。 |
| `commands.json` | Mvvm | 命令入口和动作元数据。 |
| `eventbus.json` | EventBus | handler、可发布事件、可订阅事件。 |
| `data.json` | Data | HTTP/gRPC/SignalR client 贡献。 |
| `localization.json` | Localization | 语言包、资源索引、fallback 信息。 |
| `settings.json` | Core Configuration | 设置 section、schema、设置页面入口。 |
| `diagnostics.json` | Diagnostics | 诊断 provider 元数据。 |

### 4. ContributionId

每个 Contribution 必须有稳定 Id。

推荐格式：

```text
<plugin-id>:<module-id>:<contribution-type>:<local-id>
```

规则：

- ContributionId 必须在插件内唯一。
- Host registry 可以要求在全局范围唯一。
- ContributionId 进入 lease、诊断和回滚记录。
- Source generator 应确保稳定生成。

### 5. 解析责任

PluginSystem 只负责：

- 读取索引。
- 校验文件存在。
- 记录 hash。
- 把清单交给对应模块。

具体语义由各模块解释：

- Routing 解释路由。
- Security 解释权限。
- Presentation 解释 View 和 UI 资源。
- Localization 解释语言包。
- Data 解释 client 和 connection。

PluginSystem 不解释业务语义。

### 6. 必填和可选

规则：

- `modules.json` 对包含模块的插件通常为必填。
- `routes.json`、`presentation.json` 等按插件能力决定是否必填。
- 可选清单缺失不应导致插件验证失败。
- 清单存在但内容无效时，由所属模块决定是否使插件启用失败。

### 7. Hash 和审计

每个贡献清单应记录：

- 路径。
- hash。
- 生成工具版本。
- 生成时间可作为非核心诊断信息。
- 所属插件和版本。

hash 写入安装记录或锁定文件，用于判断安装内容是否被篡改。

### 8. 测试要求

必须覆盖：

- required 清单缺失。
- optional 清单缺失。
- 清单路径越界。
- ContributionId 重复。
- 清单 hash 变化。
- 各模块只解析自己的清单。
- PluginSystem 不执行插件代码即可完成索引读取。
