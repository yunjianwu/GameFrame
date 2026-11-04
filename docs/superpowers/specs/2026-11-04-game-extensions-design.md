# 原项目通用模块迁移设计

用户已确认：将已分析的引导、Spine、UI 列表、粒子预算及常用 UI 控件迁入本工程。原项目只读；新工程可直接使用，含中文接入说明、来源映射和演示。

## 边界与选型

采用独立源码程序集：`Starter.Extensions.UI`、`Starter.Extensions.Tutorial`、`Starter.Extensions.Spine`、`Starter.Extensions.Effects`。前三方 Spine 4.1 源码、编辑器和 Shader 放 `Assets/ThirdParty/Spine`，保留原版权头；基础框架和 UI 不引用 Spine。沿用 Built-in / Unity 2022.3 / UGUI，不引入 Lua、平台 SDK 或 DOTween Pro。

直接复制全部旧脚本会引入原游戏依赖；完全重写会丢失现有控件能力。这里保留可独立的 UI 源码和交互表现，重构依赖业务的引导、Spine 播放与粒子生命周期。源脚本名称和新接口的对应关系写入迁移清单。

## 引导

使用 ScriptableObject 配置、稳定字符串步骤 ID、目标注册表、点击/拖动/业务事件/延时步骤、前置事件条件、对白、可跳过、进度存储接口。UI 点击步骤自动绑定 Button；棋盘合成和订单提交由业务上报事件，不依赖 MergeGrid 或 GameManager。切步/关闭统一取消等待并解绑；目标等待有超时，目标消失重新等待；展示使用非缩放时间。保留遮罩、目标跟随和两点手指动画。多步骤并行抢占、联网同步存档、编辑器节点图不在本次范围。

## Spine 与动作序列

统一 SkeletonGraphic / SkeletonAnimation 的播放入口、轨道、起始时间、片段、速度、皮肤、倒放、动画事件与完成状态；完成/打断/取消有明确结果并解绑。提供入场转循环、透明度过渡、Slot 透明度同步及可配置动作序列。动态 UI 透明度只由 CanvasGroup 控制，避免重复相乘。示例骨架由本工程生成，不复制原游戏商业人物资源。

## UI

保留原循环列表、翻页、分组展开、行内 Tips 能力，移除 DOTween 依赖并修正重复初始化和回收问题。迁入 Tab、RadioButton、UIEventListener、按钮缩放、CircleImage、RoundRectangleImage、CustomImage、BrokenLineImage、渐变、描边、字间距、弧形文字、不换行文本、打字机、富文本/超链接/内嵌图片、UI 排序、折线、点击透传和遮罩。富文本精灵由调用方提供同步解析器，避免异步回调污染复用控件。自定义字体下载、原游戏头像/3D模型资源服务和平台字体接口由业务自行接入。

## 粒子预算

保留优先级、可见性、距离、未模拟时间补偿和强制更新机制；使用正确的 OnEnable/OnDisable 注册周期、复用排序列表、统一时钟与预算计时，不保留 Lua 日志。预算是软上限，单次 Simulate 可能超过预算，需真机测量。

## 验收

新增生命周期、引导取消/恢复/目标延迟、Spine 打断/片段/透明度、UI 池化与重复绑定、粒子注销的回归测试。保留既有 Boot 验证；运行 Unity EditMode/PlayMode，生成并验证扩展示例场景，执行 Windows64 开发包构建。日志、测试与构建产物全部输出到 `E:\AI\_DownLoads\unity-gf-starter`。无图形测试不能替代真机渲染和性能验收。
