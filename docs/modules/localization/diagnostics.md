# AtomUI.City.Localization Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Code | 名称 | 语义 | 关键上下文 |
| --- | --- | --- | --- |
| AUCLOC001 | ResourceMissing | 当前 culture 和 fallback chain 均未找到 key。 | cultureName、resourceKey、errorKind。 |
| AUCLOC002 | FallbackMissing | fallback 查找失败。 | cultureName、fallbackCultureName、resourceKey。 |
| AUCLOC003 | PackageLoadFailed | 语言包加载失败。 | cultureName、packageId、scope、errorKind。 |
| AUCLOC004 | AtomUiApplyFailed | Presentation bridge 应用 culture 失败。 | cultureName、errorKind。 |
| AUCLOC005 | MessageFormatFailed | 本地化消息格式化失败。 | cultureName、resourceKey、errorKind。 |
| AUCLOC006 | TextRefreshFailed | LocalizedText 刷新失败。 | cultureName、resourceKey、errorKind。 |
| AUCLOC007 | CultureChanged | culture 切换提交成功。 | cultureName、fallbackCultureName。 |
| AUCLOC008 | CultureSwitchRejected | culture 切换被拒绝。 | cultureName、fallbackCultureName、errorKind。 |
| AUCLOC009 | CultureSwitchSkipped | 重复设置当前 culture。 | cultureName、fallbackCultureName。 |
| AUCLOC010 | PluginPackagesRevoked | 插件或模块 contribution 的语言包已撤销。 | cultureName、contributionId、revokedPackageCount、errorKind。 |

## 产品级必须诊断的失败

- 语言包格式错误：跳过并诊断。
- 缺失 key：返回 fallback 或 key。
- culture 切换部分 target 失败。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Localization.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
