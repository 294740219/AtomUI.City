# AtomUI.City.Routing Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

当前源码没有模块专属诊断 ID。产品级实现如果新增诊断，必须先在本文件登记。

## 产品级必须诊断的失败

- 模板语法错误：graph build 失败。
- 路由冲突：拒绝发布 graph。
- 参数绑定失败：NavigationResult Failed。
- Guard 拒绝或重定向。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Routing.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
