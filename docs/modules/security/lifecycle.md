# AtomUI.City.Security Lifecycle

## 生命周期范围

执行边界：Host runtime security service。

AtomUI.City.Security 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- Authentication: Unknown -> Anonymous 或 Authenticated -> Refreshing -> Authenticated 或 Anonymous
- Authorization: Created -> Evaluating -> Succeeded 或 Failed

## 生命周期流程

- AuthenticationStateStore 发布 snapshot。
- PermissionRegistry 注册 permission。
- AuthorizationEvaluator 评估 policy。
- RouteGuard 映射 result。

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

- 权限未注册：授权失败。
- policy provider 缺失：返回 Failed。
- token provider 不可用。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
