# AtomUI.City.Routing ViewModel Target

`ViewModelTargetDescriptor` 是 Routing 与 Presentation 的边界数据：

- `ViewModelType`
- read-only `ParameterBindings`
- optional `ReuseKey`
- optional `ActivationHint`

Generator 从 route attribute 和 typed parameter binder 生成 descriptor。Graph 要求 Route/Layout/Index 具有 target；导航要求 target 是 concrete closed class。

Routing 只验证和发布 descriptor，从不调用构造函数、不解析 ViewModel service、不定位 View。Presentation 决定如何创建 ViewModel、解释 ActivationHint、使用 ReuseKey 和提交 UI。

`NavigationSnapshot.ReuseKey` 原样暴露 descriptor 的 key；它不是 ViewModel 缓存实现。
