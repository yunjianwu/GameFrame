# 原项目通用功能迁移清单

来源根目录：`D:\MergeCookAndroid`。目标根目录：`D:\workspace\UnityGFStarter`。迁移方式包括原源码适配、通用重构与接口合并；并非把带有合并玩法依赖的原脚本原封不动复制过来。

## Procedure 流程

原目录：`Assets/UnityGameFramework/GameFrameworkExtend/Procedure`。新目录：`Assets/Game/Runtime/Procedures`，启动支持在 `Assets/Starter/Runtime/Startup`。详细接线、配置与 JSON 见 `procedures.md`。

| 原脚本 | 新位置与处理 |
|---|---|
| ProcedureBase | 同名项目基类：UseNativeDialog 有实际默认值，增加当次进入检查；停止声音/隐藏实体保留，异步卸载由 ChangeScene 等待完成 |
| ProcedureLaunch | 同名流程 + ProcedureBoot：保留启动边界和资源模式初始化，Lua 与微信/字节 SDK 改为项目接入点 |
| ProcedureCheckVersion | 同名流程：用配置化 HTTP(S) JSON 与官方 GF CheckVersionList / UpdateVersionList 代替 LuaGame 及原服务器参数 |
| ProcedureUpdateResource | 同名流程 + ResourceUpdateCoordinator：资源检查、全量/按组顺序下载、确认、进度和有限重试；不依赖原工程定制 GF 方法 |
| ProcedureEnterGame | 同名流程 + Preload：业务配置先加载，进入游戏时请求首业务场景；Lua 初始化作为可选扩展 |
| ProcedureLoadLuaScripts / ProcedureExecLuaScripts | 原实现为未完成或空壳，不复制失效类；需要 Lua 时在 Launch 初始化虚拟机、Preload/EnterGame 加载执行 |
| 新增 ProcedureChangeScene | 完整等待旧场景卸载与目标场景成功；Menu/Play 通过同一入口切换，常驻 Boot 不参与卸载 |

原工程各 UseNativeDialog 实现抛 NotImplementedException，且资源更新调用包含项目私有接口，直接复制无法接到官方 GF。本次保留可复用职责并换成目标版本实际支持的 API，默认不依赖远端服务。

## 引导与动画

| 原路径/脚本 | 新位置与处理 |
|---|---|
| Assets/Script/CS/QuickPlay/Tutorial/MergeConfigurableTutorial 及步骤逻辑 | Extensions/Tutorial：TutorialConfig、TutorialRunner、TargetRegistry、Target、InputGate、ProgressStore。保留配置驱动、目标等待、点击/拖动/事件、对白和进度能力；MergeGrid、第二单跳步等规则改为业务上报信号 |
| Assets/Script/CS/QuickPlay/Tutorial/FingerPointer.cs | Extensions/Tutorial/FingerPointer：保留点按和两点滑动，集中在 LateUpdate 更新，支持不同 Canvas 坐标转换 |
| Assets/Script/CSToLua/HollowOutMask.cs | Extensions/UI/HollowOutMask + Tutorial/TutorialMask：矩形挖洞、跟随、多目标、点击过滤，取消 Lua 依赖 |
| Assets/Script/CS/QuickPlay/Merge/LuaSpineAnimation.cs 及 Lua 播放封装 | Extensions/Spine/SpinePlayer + SpinePlayback：统一轨道、秒数、循环、片段、倒放、队列、皮肤、事件和结束状态 |
| Assets/Script/CS/QuickPlay/Tutorial/RewardUISpineController.cs | Extensions/Spine/SpineIntroLoop：show → idle，缺动画回退，禁用解绑 |
| Assets/Script/CS/Utils/SlotAlphaFollowerGraphic.cs | 同名通用组件：只写 CanvasGroup.alpha，避免再次写 Text.color.a 造成 alpha 平方 |
| Assets/Script/CS/Utils/SpineTransition.cs | 同名通用组件：非缩放时间、终点写入、重播取消旧等待 |
| 原剧情顺序播放与 Lua 动画事件转发 | Extensions/Spine/SpineSequence + UI/AnimationEventRelay：动作/等待/事件配置与超时，剥离 Lua 管理器 |
| Assets/Spine | Assets/ThirdParty/Spine：完整本地 Runtime/Editor/Shader/meta，增加 UGUI 程序集引用，保留上游版权头 |

## UI 控件

原目录主要为 `Assets/Script/UGUIExtend`；目标为 `Assets/Extensions/UI/Runtime`，对应 Editor 文件位于 `UI/Editor`，统一命名空间 `Starter.Extensions.UI`。

| 原控件 | 处理与备注 |
|---|---|
| CircleImage、RoundRectangleImage | 迁入，补零填充、细分边界与圆角范围处理 |
| CustomImage、BrokenLineImage | 保留图形扩展与编辑器 |
| CircleText、GradientColor、GraphicOutline、TextSpacing | 保留弧形文字、渐变/曲线/逐行效果、描边、字间距；日志去 Lua |
| Assets/Script/CS/Utils/TextGradient.cs | 迁入多色分段渐变，与 GradientColor 并存 |
| TextNonBreakingSpace、CTextLineFeed | 保留不换行空格与中文换行辅助 |
| Tab、TabGroup、RadioButton | 保留选择控件；修复绑定初始状态、程序切页、禁用点击 |
| UIEventListener | 保留 Pointer/Drag/Scroll 回调入口 |
| UIBtnScaleEffect | 重构为非缩放时间插值，保留非均匀原始缩放，禁用和移出恢复 |
| CommonStoryText | 重构打字机：不截断富文本标签，支持立即显示、取消和重复播放 |
| RichText | 保留图片/表情序列帧/超链接/下划线语法，以实例 SpriteResolver 替代 GameEntry/CAssetManager |
| LinkImageText | 保留兼容接口与静态 funLoadSprite，移除原资源服务；新业务推荐 RichText |
| CSetUIOrder | 保留局部 Canvas 排序，避免重复添加 GraphicRaycaster |
| CInfinityLoopComponent | 保留原固定池实现与层级约定；推荐新代码使用 UILoopScrollView |
| Scroll/UILoopScrollView | 保留虚拟列表与尺寸接口，去 DOTween，修复横向尺寸、索引与重复初始化 |
| Scroll/FlipPageLoopScrollView | 保留翻页与导航点，新增明确的 InitPaged，重建时重置页表 |
| Scroll/ExpandLoopScrollView | 保留组折叠，补回调保存、Header 池重入，移除重复 Cell 点击绑定 |
| Scroll/ExpandTipsLoopScrollView | 保留行内详情，使用基类单次订阅 |
| Assets/Script/UnityExtend/UILineRenderer.cs | 通用化 SetPoints/DrawLine，去运行时鼠标测试逻辑 |
| Assets/Script/CSToLua/ClickPassDown.cs | 去 Lua，点击只转发一次，避免 PointerClick + Submit 双触发 |
| UI/CanvasGroupFade | 新增独立 UI 透明度工具，无 DOTween 依赖 |

## 粒子

| 原路径 | 新位置与处理 |
|---|---|
| Assets/ParticleBudget/ParticleSimulationBudgetManager.cs | Extensions/Effects：保留预算/优先级/补偿，复用排序缓存，统一时钟与计时；无相机也运行 |
| Assets/ParticleBudget/ParticleSystemController.cs | 注册/注销与启用周期一致，退出托管恢复原播放状态；不自动接管子粒子 |
| Assets/Script/CS/Utils/ParticleTransition.cs | Extensions/Effects/ParticleTransition：补层级空节点、非缩放时间、终点与原颜色/渐变保留。只改新发射粒子的 startColor |

## 保留在业务层的内容

CustomText 中的原平台字体下载/ProjectSetting 依赖以标准 UGUI Text 及独立字体资产替代；没有把原微信字体服务带进工程。角色、贴图、活动配置、Spine 游戏素材、头像/3D 模型加载服务、MergeGrid/GameManager、发奖/订单逻辑、Lua/ToLua、广告/登录/支付、微信/抖音 SDK 不属于通用模块。

Spine 本地插件自带的骨骼跟随、Slot 跟随、换装、渲染分离、动画事件等待等工具随插件保留。项目专用的 SpineStaticBaker、ParticleExporter 和平台导出流水线未接入通用启动链路；它们需要单独确认导出约束，不能当作已经适配的新项目构建工具。

原 UI 中保留的作者声明仍在源码里。原游戏代码没有因此获得 GF 的 MIT 许可证，对外分发需结合原项目权属；详见 THIRD_PARTY_NOTICES.md。
