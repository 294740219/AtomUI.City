# AtomUI.City.Testing Lifecycle

## 生命周期范围

执行边界：Test-only framework boundary。

Testing 可以创建可控 Host，但生产 Host 不得依赖 Testing 包。

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

- 测试结束必须 dispose TestHost、dispatcher、scheduler 和插件测试对象。
- pending work、未释放 disposable 和 generated snapshot mismatch 必须让测试失败。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
