# Unity GF Starter 设计

这是一套给 Unity 新项目使用的 GF 源码底座及最小业务示例。基准编辑器为 Unity 2022.3 LTS，默认使用 Built-in 渲染管线，业务代码用 C#。

## 取舍

采用官方 GameFramework / UnityGameFramework 的 `v2021.05.31` 配套源码，而不是搬运原游戏修改后的资源系统。保留完整框架便于后续使用 Entity、Sound、Network 等模块；默认业务只启用必要能力。完整框架的保留不意味着每个项目都应使用所有模块。

直接复制原游戏虽然更接近现有操作习惯，但会带入加密格式、Lua 加载约定和平台 SDK。重写一套全部自研的框架会增加长期维护成本。本方案保留 GF 的成熟实现，把新项目规则放在独立 Starter 程序集中。

## 目录与责任

- `Assets/Framework`：带上游许可证的 GF 源码。仅为去除旧压缩 DLL 依赖、兼容当前 Unity 所必需的变更在此发生。
- `Assets/Starter/Core`：不依赖 Unity 的主线程队列、事件订阅作用域和示例数据模型，可单独测试。
- `Assets/Starter/Runtime`：GF 入口、启动流程、UI 分层辅助器、资源名称约定和生命周期管理。
- `Assets/Game`：可替换的示例业务、演示界面与配置。
- `Assets/Starter/Editor`：首次生成示例场景、预设及配置的菜单；资源构建与验证入口。
- `Assets/Starter/Tests`：通用模块回归测试和场景集成测试。
- `docs`：接入、扩展、线程边界、架构意见和验证记录。
- `tools`：不要求全局安装依赖的本地验证脚本。

## 运行链路

`Boot.unity → GF 组件 Awake/Start → ProcedureBoot → ProcedurePreload → ProcedureMenu ↔ ProcedurePlay`。

Boot 只做组件获取和本地服务初始化；Preload 等资源系统可用并加载示例配置；Menu / Play 只管理所属界面与业务状态，离开流程时关闭或取消自己申请的界面。资源失败进入可见错误状态，避免悄悄停在黑屏。

## 执行模型

框架入口、业务状态、UI、事件订阅、GF 对象池与 Unity API 默认都在主线程操作。Unity 协程用于等待和分帧，不用于后台计算。后台计算只接受普通 C# 数据，通过有容量限制的主线程队列提交结果。WebGL 路径使用协程，不依赖 `Task.Run`。

事件参数通过 GF ReferencePool 获取并在派发后归还，订阅者不能保留参数对象。界面每次打开创建订阅作用域，每次关闭释放订阅，兼容 GF 的界面池复用。

## 验收

1. 独立工程内不引用原游戏命名空间、Lua 绑定、SDK 或凭据。
2. 纯 C# 测试验证回调顺序、跨线程提交、销毁后拒绝提交、容量边界、异常隔离、事件解绑和数据校验。
3. Unity 实际导入编译，生成并保存可打开的示例场景及 UI 预设。
4. EditMode / PlayMode 检查关键行为，记录实际执行结果与未覆盖平台。
5. 所有构建、日志、测试报告输出到 `E:\AI\_DownLoads\unity-gf-starter`；Unity 运行缓存通过目录联接放在该目录。
