# AtomUI.City.PluginSystem Lifecycle

## 生命周期范围

执行边界：Host runtime plugin boundary。

AtomUI.City.PluginSystem 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- Install: NotInstalled -> Staging -> Installed 或 RolledBack
- Runtime: Discovered -> Loaded -> Enabled -> Disabling -> Disabled -> Unloading -> Unloaded
- Unload failure: Unloading -> UnloadPending -> Unloaded
- Contribution: Declared -> Applied -> Revoking -> Revoked

## 生命周期流程

- Install 校验 package layout、manifest、签名、依赖和路径。
- Load 创建插件运行时 owner，读取 manifest 和主 assembly。
- Enable 应用 module 和 contribution manifest，不修改 Host root provider。
- Disable 取消插件 operation，撤销 route/view/state/event/resource/data/security contribution。
- Unload 释放插件对象并验证 remaining references。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- PluginSystem 管理插件完整状态机：Install、Load、Enable、Disable、Unload。
- 卸载时必须撤销所有 contribution lease。
- 卸载失败进入 UnloadPending 或失败结果。

## 异常中断行为

- manifest 缺失或 schema 不兼容：拒绝安装或加载。
- 依赖缺失或版本不满足：插件保持 Installed，不进入 Enabled。
- 贡献撤销失败：继续撤销其他贡献并报告 UnloadPending。
- 路径穿越或包布局非法：拒绝安装并清理 staging。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
