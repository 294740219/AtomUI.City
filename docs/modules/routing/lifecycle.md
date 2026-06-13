# AtomUI.City.Routing Lifecycle

## 生命周期范围

执行边界：Host runtime navigation graph。

AtomUI.City.Routing 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- RouteGraph: Building -> Validated -> Published -> Superseded
- Navigation: Created -> Matching -> Guarding -> Resolving -> TargetReady -> Committed 或 Cancelled 或 Failed

## 生命周期流程

- Route declaration 进入 graph builder。
- RouteMatcher 在 immutable graph 上匹配。
- NavigationScope 执行 guard 和 resolver。
- 成功输出 NavigationTarget。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 模板语法错误：graph build 失败。
- 路由冲突：拒绝发布 graph。
- 参数绑定失败：NavigationResult Failed。
- Guard 拒绝或重定向。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
