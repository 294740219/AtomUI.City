# AtomUI.City.Build Lifecycle

## 生命周期范围

执行边界：MSBuild and repository engineering boundary。

本模块不作为 Host 运行时服务。它通过 MSBuild props/targets 和工程测试约束 Host 包和应用包的生成。

## 模块特有状态机

- Created -> Executing -> Completed 或 Failed
- Failure -> Diagnostics 或 non-zero gate result

## 生命周期流程

- 读取输入。
- 验证输入和边界。
- 执行模块动作。
- 输出结果和诊断。
- 测试或 CI 断言结果。

## Host Shutdown / 执行结束行为

- MSBuild target 失败必须让构建失败。
- output 和 package 产物由 clean/rebuild 管理。
- gate 失败不能产生 publish candidate。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
