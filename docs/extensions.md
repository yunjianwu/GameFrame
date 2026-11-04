# 引导、Spine、粒子与常用 UI

工程入口为 `D:\workspace\UnityGFStarter`。扩展源码位于 `Assets/Extensions`，原游戏只用于读取和提取；接入新项目不需要原游戏目录、Lua 或 DOTween。

## 先运行展示

使用 Unity 2022.3 LTS 打开工程，执行 `Starter/5. Create or Open Extension Gallery`，点击 Play。也可以直接打开 `Assets/Extensions/Samples/Scenes/ExtensionGallery.unity`。默认启动场景仍为 `Assets/Game/Scenes/Boot.unity`。

| 页签 | 可以操作什么 |
|---|---|
| TUTORIAL | Restart → 点击 Confirm → 拖到 Drop Here → Claim Reward；Stop 取消；Resume 读取当前运行期间的下一步 ID |
| UI | 圆形、圆角、折线、文字渐变/描边、内嵌图片/超链接、打字机、Tab |
| LISTS | 1,000 项虚拟列表、8 页水平列表、分组展开、行内详情；点击项查看底部反馈 |
| SPINE | show 入场转 idle、show 重播两次再循环、透明度过渡、1x/2x 速度、骨骼跟随文字与 Slot 透明度 |
| EFFECTS | 三种优先级发射器的预览、注册数量/模拟耗时、预算调度与 Unity 自动更新切换 |

`Assets/Extensions/Samples/Prefabs` 提供 BasicButton、RoundedCard、RichLabel、VirtualList、PagedList、ExpandableList、InlineTipsList、GuideOverlay、SpineCard。预设包含结构和组件；列表需要在业务中调用 Init/ShowList，RichLabel 需要提供图片解析器，遮罩需要 SetTarget。它们没有绑定示例场景的控制器。

场景生成器只创建缺失的整套示例。发现已有场景时验证并保留；发现部分生成资产时明确报错，不覆盖你的修改。`Starter/6. Validate Extensions` 检查配置、骨架和示例预设。

## 模块依赖

| 程序集 | 依赖 | 用途 |
|---|---|---|
| Starter.Extensions.UI | Unity.ugui | 通用 UI、遮罩、文字、列表 |
| Starter.Extensions.Tutorial | UI、Unity.ugui | 配置引导和目标注册 |
| Starter.Extensions.Spine | spine-csharp、spine-unity、Unity.ugui | 播放、动作序列、透明度 |
| Starter.Extensions.Effects | Unity 引擎 | 粒子预算与发射颜色过渡 |
| Starter.Extensions.Samples | 上述四模块 | 场景接线示例，不承担框架职责 |

业务 asmdef 显式引用需要的模块即可。新项目不使用 Spine 时，可以不引入 Spine 扩展、ThirdParty/Spine 和相关 Samples。基础 GF、UI 与引导不依赖 Spine。保留所有文件时，整个工程已经配置好程序集引用。

## 引导接入

在 Project 窗口创建 `Create > Starter > Tutorial Config`，填写 tutorialId 和 steps。步骤 id 是存档用的稳定 ID，调整步骤顺序时不要重新编号已有 ID。

在界面根上添加 TutorialTargetRegistry 与 TutorialRunner；目标上添加 TutorialTarget，填写 targetId，并关联同一个 registry。按钮自动通过 Button.onClick 报告点击；拖动在释放位置寻找同注册表中的目标。移动物品和判定合成仍由玩法代码负责。

```csharp
using Starter.Extensions.Tutorial;

// registry 和 config 来自 Inspector；一个界面一个 registry。
runner.Configure(config, registry, new PlayerPrefsTutorialProgressStore());
runner.ConfigurePresentation(mask, finger, inputGate, talkRoot, talkText);
runner.Begin(resume: true);

// 业务成功后才上报。引导不会替你执行订单、发奖或合并物品。
runner.Notify("order.submitted");

// 关界面时调用，或让挂载 Runner 的对象随界面禁用。
runner.Stop();
```

步骤类型包括 Click、Drag、Signal、Delay。prerequisiteSignal 是进入步骤前的事件条件，会记住本次 Begin 之后收到的事件；completionSignal 只完成当前已进入的 Signal 步骤，展示延时或等待目标期间收到也会保留。连续步骤使用相同的 completionSignal 时，每步仍需重新上报一次，旧完成事件不会自动跳过后续步骤。事件要用稳定业务名称。targetTimeoutSeconds 限制等待前置条件、目标出现/重现的时间，不会强迫正在操作的玩家在 15 秒内完成点击或拖动。

目标先隐藏后显示时，Runner 会解绑并重新等待；Stop、切步、禁用会取消旧协程与订阅。State、Error、StepEntered、StepCompleted、Completed、Failed 可用于 UI 和日志。SkipCurrent 只对标记 skippable 的当前步骤生效。

内存存档适合临时界面流程；PlayerPrefs 存档适合本地新手引导；联网存档实现 ITutorialProgressStore。保存的是“下一步 ID”，空字符串表示完成。存档中的 ID 被配置移除时会明确失败，请在版本更新时做迁移或重置该引导的存档。

TutorialInputGate 仅提供白名单判断，业务输入处需要主动调用 `gate.Allows(targetId)`。遮罩拦截 UI Raycast，不会自动拦截键盘、物理射线或直接调用业务方法。两个目标的视觉洞是外接矩形；只有实际目标矩形放行点击，两目标之间仍阻挡交互。手指支持点按、双向滑动和重复单向滑动，使用非缩放时间。

## 列表接入

推荐从 VirtualList 等预设复制结构：ScrollRect → Content → Cell。Viewport 指向列表自身；Content 和 Cell 使用左上锚点。Cell 的宽高必须在 Init 前确定，自动布局时先等待布局完成或调用 Canvas.ForceUpdateCanvases。

```csharp
list.Init((cell, index) => {
    // 回调 index 为 1-based；数据数组需要减 1。
    cell.GetComponentInChildren<UnityEngine.UI.Text>().text = data[index - 1].name;
}, (cell, index) => Select(data[index - 1]));
list.ShowList(data.Count);
list.UpdateCell(1);    // 第 1 项
list.UpdateList();     // 数量不变时重绑已显示项
list.ToItem(0);        // 此旧接口特意保留 0-based，表示第 1 项
```

保留旧索引规则方便对应原工程；新业务建议自己封装统一索引，不要混用。

| 控件 | 初始化/刷新 | 特别备注 |
|---|---|---|
| UILoopScrollView | Init(bind, click)、ShowList(count) | 只实例化可见项和少量边缘项；数量变化后重新 ShowList |
| FlipPageLoopScrollView | InitPaged(bind, pageChanged)、ShowList(count) | 推荐水平、每页 1 项；SetToPageIndex/CurrentPage 和页回调为 1-based |
| ExpandLoopScrollView | Init(bind4, click4)、ShowList("3\|4\|2") | 需要 m_ExpandButton 模板；回调是 header/cell/group/item，组与项为 1-based；header 刷新时 cell=null、item=-1 |
| ExpandTipsLoopScrollView | Init(bind, click)、ShowList(count) | 需要 m_ExpandTips，推荐单列纵向；点击回调里更新详情内容 |
| CInfinityLoopComponent | 原 SetAmount/回调接口 | 兼容旧的固定池布局：挂在 Content，父级为 Viewport，祖父级为 ScrollRect；需要 GridLayoutGroup、ContentSizeFitter 和足够的预建 Cell。新项目优先用 UILoopScrollView |

可变尺寸列表要求 m_Row=1，尺寸数组与数量一致。列表内部位移动画使用自有协程，禁用会取消。Cell 中的业务按钮请绑定一次，再读取当前数据；不要在每次 bind 时重复 AddListener。各派生列表的复杂多行、动态布局组合仍需要按业务验证，展示提供的是推荐组合。

## 文字和交互

RichText 支持 `<#coin>`、`<quad name=coin size=30 width=1 />`、`<a=reward.details>详情</a>`、`<uline>下划线</uline>` 以及原来的表情序列帧格式。使用同步解析器消除原来的资源管理器耦合：

```csharp
richText.SpriteResolver = key => spriteCache.TryGetValue(key, out var sprite) ? sprite : null;
richText.onHrefClick.AddListener(key => OpenPanel(key));
richText.text = "Reward <#coin> <a=reward.details>details</a>";
richText.RefreshSprites();
```

异步资源请在业务层提前加载到缓存，再 RefreshSprites。解析器不转移资源所有权，调用方负责释放资源；不要在解析器中每次 Sprite.Create。LinkImageText 保留静态 funLoadSprite 兼容入口；新代码推荐实例级 RichText 解析器，避免跨界面污染。超链接只抛出事件，不自动访问外部网址。

CommonStoryText 的 PlayAni(text, callback)、ShowText()、StopAni() 支持重播、立即显示、取消；完成回调只触发一次。它控制生成文字的顶点可见度，富文本标签不参与逐字拼接。

CircleImage、RoundRectangleImage、CustomImage、BrokenLineImage、UILineRenderer 提供图形；GradientColor、TextGradient、GraphicOutline、TextSpacing、CircleText 提供文字效果。GradientColor 支持方向、曲线、逐行渐变；TextGradient 保留多色分段渐变。复杂描边会增加顶点量，长列表不宜给每个小字都加多层效果。

Tab/TabGroup、RadioButton、UIEventListener、UIBtnScaleEffect 提供选择与交互。按钮缩放保留原始非均匀缩放，释放、移出、禁用后恢复；Tab 支持普通 ToggleGroup 和代码设置 isOn。SetIsOnWithoutNotify 本来就跳过值变化回调，若业务使用它，请自行同步页面。

CanvasGroupFade 处理 UI 淡入淡出；CSetUIOrder 处理局部 Canvas 排序；ClickPassDown 只转发一次 PointerClick，使用前确认层级确实需要透传。AnimationEventRelay 将动画事件转成 UnityEvent/C# 事件，无 Lua 接口。

## Spine 接入

本地携带 spine-unity 4.1.43 / spine-csharp 4.1.2 的 Runtime、Editor 和 Shader。素材应使用匹配的 Spine 4.1 导出格式；不要把 4.2 骨架直接交给本包。许可证说明见 THIRD_PARTY_NOTICES.md。

在 SkeletonGraphic 或 SkeletonAnimation 上添加 SpinePlayer，或 Configure 指向对应组件。

```csharp
var playback = player.Play("attack", trackIndex: 0, startTimeSeconds: 0.2f,
    completed: result => UnityEngine.Debug.Log(result));
player.Queue("idle", loop: true);
playback.AnimationEvent += e => OnAnimationEvent(e.Data.Name);
// 如果当前轨道已换成别的动画，取消旧句柄不会清除新轨道。
playback.Cancel();
```

Play 的 trackIndex 是轨道编号，startTimeSeconds 是起播秒数，不能把第二个整数误当起播时间。支持 reverse、segmentStartSeconds/segmentEndSeconds；SetSkin 返回是否成功；SetSpeed 设置动画状态速度；PlayLoop 可先播放 `name_start` 再接循环。

非循环播放结果为 Completed、Interrupted、Cancelled 或 MissingAnimation。循环动画持续持有句柄，单圈通知通过 CycleCompleted。回调可能因替换、关闭而触发，请按结果分支；可以在完成/中断回调中再次调用 Player.Play，Player 会先完成新句柄的事件订阅，再派发旧句柄的用户回调。禁用/销毁 SpinePlayer 会取消其句柄。若单独禁用被引用的 SkeletonGraphic 而仍保留 Player 启用，业务应同时 CancelAll，避免等待未继续更新的动画。

SpineIntroLoop 自动 show → idle，缺少 show 时回退 idle。SpineSequence 支持重复动画、loopAfter、Delay、WaitForSignal、Invoke 与 UnityEvent；Play/Stop/Notify 控制，超时或打断会失败并清理自己启动的动画。正常完成后 loopAfter 继续播放，直到替换或 Stop。跨组件并行剧情由上层流程管理，不应让多个序列同时争夺同一轨道。

SlotAlphaFollowerGraphic 读取指定 Slot.A 到 CanvasGroup.alpha；Text.color.a 保持原值，避免透明度乘两次。位置/旋转跟随使用本包的 BoneFollowerGraphic。SpineTransition 淡入淡出 SkeletonGraphic，外部挂件需要一起淡出时，在共同父节点使用 CanvasGroupFade。

## 粒子接入

场景放一个 ParticleSimulationBudgetManager，在需要托管的每个 ParticleSystem 上添加 ParticleSystemController。控制器只处理自身系统，子粒子要分别挂控制器。可选择 ForceUpdate、High、Normal，普通队列按等待时间、距离与可见性排序。管理器默认使用非缩放时钟，即暂停 Time.timeScale 时仍模拟；需要随游戏暂停时在 Inspector 关闭 useUnscaledTime。

注册和注销随 OnEnable/OnDisable 进行；管理器禁用后恢复 Unity 自动模拟，再启用会重新注册。开始托管前处于暂停/停止的系统不会被擅自播放。动画特效池每次重新出池时要先按业务启动粒子，再让控制器接管。

预算是 CPU 模拟软上限：强制更新不受预算拦截；单个 Simulate 不可中途打断；不覆盖渲染、Overdraw 和 GPU 成本。建议先用 Unity Profiler 确认瓶颈再给系统加托管，避免所有小粒子都手动模拟。

ParticleTransition 保留 VnishTransition/ShowTransition 兼容方法，支持子层级与原有渐变颜色，使用非缩放时间。它改变 startColor，只作用于后续发射的粒子；已有粒子不会瞬间整体消失。需要整个特效淡出时，应改材质透明度、粒子颜色或业务专用 Shader。

## 协程与多线程

本轮扩展没有启动 C# 工作线程。引导、过渡、列表位移、剧情等待使用 Unity 主线程协程；FingerPointer、Spine 和粒子调度在 Update/LateUpdate 或 Spine 回调中运行。协程用于分帧和等待，不会把 Simulate 或 UI 网格生成搬到后台。GF 网络和资源线程边界继续参考 docs/threading.md；后台计算的结果通过 MainThreadQueue 回到主线程后再修改 UI/骨架。

本轮自动验证覆盖生命周期与交互，不等于 Android/WebGL、字体、触摸、分辨率、Shader 和性能已全部验收，实际记录见 docs/validation.md。
