# AtomUI.City.PluginSystem Signing And Trust 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Signing And Trust` 相关实现决策，不重新定义模块边界。

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

## PluginSystem 签名和信任设计

适用范围：插件包来源、签名、hash、发布者、信任策略和审计记录

### 1. 目标

插件是进程内代码，安装前必须能确认来源和内容。签名和信任系统用于决定插件是否允许安装、启用和获得能力。

设计目标：

- 包来源可追踪。
- 内容完整性可验证。
- 签名策略可配置。
- 发布者身份可审计。
- 信任结果写入安装记录和锁定文件。

### 2. 信任输入

| 输入 | 说明 |
|---|---|
| Package source | 插件源、feed、本地文件路径。 |
| Package hash | `.nupkg` hash。 |
| Content hash | 解压后运行内容 hash。 |
| Signature | 包签名或独立签名。 |
| Certificate thumbprint | 证书指纹。 |
| Publisher id | 发布者身份。 |
| PluginId | 插件运行时身份。 |
| Capabilities | 请求能力范围。 |

### 3. 签名策略

Host 可以配置签名策略：

| 策略 | 说明 |
|---|---|
| Required | 无有效签名拒绝安装。 |
| Preferred | 无签名允许安装但降低信任等级或要求确认。 |
| Disabled | 不检查签名，只检查 hash 和来源策略。 |

默认建议由应用决定。企业应用通常应使用 `Required`。

### 4. 信任等级

建议信任等级：

| 等级 | 说明 |
|---|---|
| Trusted | 来源和签名均受信任。 |
| Verified | hash 匹配，但签名策略不要求或无发布者信任链。 |
| UserAccepted | 用户显式确认安装。 |
| Untrusted | 不允许启用。 |
| Blocked | 被 Host policy 阻止。 |

信任等级影响能力授权。高风险能力可以要求 `Trusted`。

### 5. 安装时校验

```text
Resolve package source
-> Verify package hash
-> Verify signature if required
-> Check publisher policy
-> Check PluginId policy
-> Compute trust result
-> Store trust result
```

规则：

- hash 不匹配必须拒绝安装。
- 签名无效按策略拒绝或降级。
- 来源被阻止时拒绝安装。
- 信任结果必须进入 `install.json`。

### 6. 启用时复核

启用插件前应复核：

- 安装记录存在。
- content hash 未变化。
- 清单 hash 未变化。
- 锁定文件来源和安装记录一致。
- 信任结果仍满足 Host policy。

如果安装后文件被修改，插件必须进入 Invalid 或 Disabled。

### 7. 审计记录

审计记录应包含：

- operation id。
- package source。
- PluginId。
- PackageId。
- Version。
- package hash。
- content hash。
- signature status。
- certificate thumbprint。
- trust level。
- user/admin decision。

### 8. 非目标

第一版不承诺：

- 进程内不可信代码沙箱。
- 插件市场服务端。
- 远程吊销检查。
- 自动证书生命周期管理。

这些能力可以在后续安全模块或企业策略模块中扩展。

### 9. 测试要求

必须覆盖：

- 有效签名。
- 缺失签名。
- 无效签名。
- hash 不匹配。
- 不可信来源。
- 用户确认安装。
- 信任等级影响能力授权。
- 安装后文件被篡改。
