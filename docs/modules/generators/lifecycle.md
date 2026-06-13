# AtomUI.City.Generators Lifecycle

## 生命周期范围

执行边界：Roslyn compile-time analyzer boundary。

本模块不进入 Host 运行时容器。Host 只消费 generator 生成的 manifest、registrar 或编译期诊断结果。

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

- 编译结束后不保留运行时对象。
- generator 不得依赖 Host shutdown 清理资源。
- 失败必须通过 Roslyn diagnostics 表达。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
