# AtomUI.City.State Lifecycle

## 生命周期范围

执行边界：Host runtime state manager。

AtomUI.City.State 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- StateScope: Created -> Active -> Disposing -> Disposed
- WritableState: Registered -> Active -> Disposed
- StateCollection: Registered -> Active -> Disposed
- Subscription: Active -> Disposed
- Snapshot: Created -> Immutable

Active 为构造后的初始可观察态（Created 是构造前概念态）。StateScope/ComputedState 的订阅释放失败不改变终态——Disposing 后无论是否失败都到达 Disposed，失败仅记录诊断（`AUCSTA009`/`AUCSTA010`）。

## 生命周期流程

- Register 定义 StateKey、lifetime、default value、access policy 和 snapshot policy。
- Set 检查 access policy，原子更新 value 和 version。
- Notify 根据 dispatch policy 调用订阅者。
- Snapshot 按 scope/policy 捕获不可变条目。

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

- 未注册 key：抛 StateNotRegisteredException 并诊断。
- 写入只读 state：抛 StateAccessDeniedException。
- 订阅回调失败：记录 subscriptionId，不回滚已提交状态。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
