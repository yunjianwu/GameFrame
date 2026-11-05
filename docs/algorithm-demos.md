# 三个算法 Demo 与 GF 的整合

新工程：`D:\WorkSpace\UnityGFDemos`。框架来源为 `D:\WorkSpace\UnityGFStarter`；算法来源为 `D:\WorkSpace\wechat-article-demos\demos-unity` 下的三个项目。源工程保持独立，本工程维护自己的接入与适配代码。

## 运行与操作

使用 Unity 2022.3.62f2 打开工程，进入 `Assets/Game/Scenes/Boot.unity` 后按 Play。安装目录名是 `2022.3.62f2`，本机编辑器实际报告 `2022.3.62f2c1`。渲染管线为 Built-in，默认使用旧 Input Manager，不需要新 Input System 包。

菜单三个卡片分别进入 Boids、Flow Field、QuadTree。顶部 `< HUB` 与 `Esc` 返回菜单；`RESET` 卸载并重载当前 Demo 场景，恢复场景初始参数。普通调参只影响本次运行，没有额外存档。右上角 `FRAMEWORK SAMPLE` 是原框架金币示例，其存档仍按原框架规则处理。

### Boids 鸟群

- 左上角可切换随机运动、分离、对齐、凝聚四个阶段。
- 可调整鸟数、感知半径、三种权重及拖尾。
- 在场景空白处按住左键吸引，按住右键驱散。
- 默认 200 只，保留原始全量邻居搜索，复杂度随数量近似平方增长；适合解释规则和作为四叉树优化前的对照。

### Flow Field 流场寻路

- 右侧五个阶段依次展示代价场、积分场、向量场、个体查表移动和群体分离，默认打开最后一阶段、2,000 个体。
- 选择画墙、沼泽或擦除后，在地图内左键拖动；右键移动绿色终点。
- 改地图和终点后会重建流场，个体继续共享方向数据。
- `A* 对照` 在主线程同步执行多次搜索，按钮点击时可能短暂卡顿；它展示计算模型差异，没有后台任务调度。
- `重置地图` 只重置地图；顶部 `RESET` 则重载整个 Demo。

### QuadTree 四叉树

- 默认进入四叉树实验室：点击空白处加点，拖动已有点，观察节点分裂和鼠标范围查询。
- 切换鸟群模式，可修改数量、节点容量、感知半径，切换四叉树或暴力搜索，并选择是否显示树结构。
- 鸟群模式中左键吸引、右键驱散，默认 1,000 只；数量上限不是性能保证。
- 本工程增加最大深度和极小节点保护，重复位置的点保留在终止节点，避免持续分裂导致栈溢出。

三套算法更新目前都在 Unity 主线程的 `Update` 中执行。动态 Mesh 合并减少绘制提交，但没有使用 Jobs、Burst、计算着色器或工作线程。GF 启动、资源和场景异步加载沿用底座的生命周期；不要把异步加载等同于算法已经多线程化。

## GF 接入方式

启动流程保留：

```text
Boot
  -> Launch -> CheckVersion -> UpdateResource -> Preload
  -> EnterGame -> ChangeScene -> Menu

Menu -> ChangeScene -> Boids / FlowField / QuadTree -> ProcedureAlgorithmDemo
Demo -> ChangeScene -> Menu
Demo -> ChangeScene (reload) -> 原 Demo
```

Boot 的框架组件保持常驻。三个 Demo 分别是 GF 管理的业务场景，切换时先卸载旧业务场景，再加载目标；不是同时创建三套常驻模拟。重置通过 `ProcedureChangeScene` 的 reload 请求完成，会销毁旧模拟、相机和绘制资源。

`MenuForm` 和 `DemoNavigationForm` 都由 GF UI 管理。按钮只在 `OnInit` 绑定一次，每次 `OnOpen` 换用当前流程上下文，`OnClose` 清空。流程回调校验 Generation，避免 UI 池复用或迟到回调再次操作旧流程。

导航 UI 的矩形注册给 `DemoInputGate`，算法鼠标按下/持续输入会避开该区域，避免点击返回时同时画墙或吸引鸟群。鼠标抬起仍传给算法，防止拖拽和画笔状态卡住。

### 关键文件

| 文件或目录 | 作用 |
|---|---|
| `Assets/AlgorithmDemos/Runtime/DemoCatalog.cs` | 三个入口、标题、场景路径和资源名 |
| `Assets/AlgorithmDemos/Runtime/AlgorithmDemoScene.cs` | 当前业务场景及模拟组件的注册，销毁时释放静态引用 |
| `Assets/AlgorithmDemos/Runtime/DemoInputGate.cs` | 导航栏输入拦截 |
| `Assets/AlgorithmDemos/Runtime/DemoPresentation.cs` | 三个 IMGUI 面板的桌面字体选择 |
| `Assets/AlgorithmDemos/Runtime/BoidsFlocking` | 鸟群算法、画面与输入 |
| `Assets/AlgorithmDemos/Runtime/FlowFieldPathfinding` | 代价/积分/向量场、A* 与人群模拟 |
| `Assets/AlgorithmDemos/Runtime/QuadTreeBoids` | 四叉树查询、实验室与鸟群优化 |
| `Assets/Game/Runtime/Procedures/ProcedureMenu.cs` | 菜单选择，发起 GF 场景切换 |
| `Assets/Game/Runtime/Procedures/ProcedureAlgorithmDemo.cs` | Demo 流程、导航、返回与重置 |
| `Assets/Game/Runtime/UI/DemoNavigationForm.cs` | 池化导航界面与输入区域的生命周期 |
| `Assets/Starter/Editor/AlgorithmDemoProjectBuilder.cs` | 场景、菜单和导航生成，Procedure 与资源接线及验证 |

算法程序集 `AlgorithmDemos.Runtime` 不依赖 GF。项目层 `Starter.Game` 引用它并负责流程接入；可以继续单独维护或复用这些算法。

### 资源分包

`Assets/GameFramework/Configs/ResourceCollection.xml` 增加以下资源；导航与现有界面共用 `demo/ui`。

| 资源名 | 资产 |
|---|---|
| `algorithms/scenes/boids` | `Assets/AlgorithmDemos/Scenes/Boids.unity` |
| `algorithms/scenes/flow-field` | `Assets/AlgorithmDemos/Scenes/FlowField.unity` |
| `algorithms/scenes/quadtree` | `Assets/AlgorithmDemos/Scenes/QuadTree.unity` |
| `demo/ui` | `MenuForm.prefab`、`PlayForm.prefab`、`DemoNavigationForm.prefab` |

编辑器以 Editor Resource Mode 直接加载资产。Windows 包内仅直接内置 Boot 和扩展示例展示场景；菜单、原金币场景和三个算法场景通过真实 GF Package 加载。新增场景必须同时更新目录、GF 资源集合和编辑器场景列表。

## 相对于原 Demo 的改动

| 原实现 | 整合后的处理 | 原因 |
|---|---|---|
| 三个 `*Bootstrap.cs` 在任意场景自动启动 | 不导入 Bootstrap，由 GF 菜单与业务场景启动 | 避免三套 Demo 一起运行 |
| 创建或改造主相机 | Demo root 自带子相机，销毁时随场景回收 | 保持 Boot 与业务相机生命周期清楚 |
| `Graphics.DrawMesh` 未限定相机 | 传入本 Demo 相机 | 避免 Boot 相机重复绘制 |
| 三个独立工程的鼠标输入 | 增加顶部导航区域拦截 | 避免导航和模拟同时响应 |
| 默认 IMGUI 字体 | 尝试 Microsoft YaHei / Noto Sans CJK SC / PingFang SC / Arial | 本机可显示中文，不复制系统字体文件 |
| 四叉树重复点持续分裂 | 最大深度 16、极小节点停止分裂，并校验容量 | 重合点仍可保存和查询 |
| 原框架竖屏示例 | 1280×720 横屏，原金币卡片缩放适配 | 为算法地图和调参面板留出空间 |
| UI 组仅在 `SetDepth` 初始化 Canvas | UI Helper 在 `Awake` 初始化零深度组 | GF 对初始深度 0 跳过回调，默认组也需要屏幕坐标和缩放 |

原 README 备份在 `docs/source-demos`。其中“直接拷贝后自动启动”的操作与原环境性能测试属于原工程说明；当前工程按本文从 Boot 进入，性能以实际运行环境为准。

## 构建与验证

Unity 菜单 `Demos/2. Validate Algorithm Demos` 检查场景注册、模拟组件、Missing Script、导航资源和 Procedure。`Starter/4. Build Windows Demo Player` 先构建 GF Package，再构建 Windows x86_64 开发包并注入 StreamingAssets；默认输出到 `E:\AI\_DownLoads\unity-gf-demos\build\packages`。

请使用该构建入口，普通的 File > Build 不包含这一外部资源注入上下文。Android、WebGL/小游戏的完整 Player 构建和输入适配尚未在本工程验收。

本轮新增的算法 EditMode、流程 PlayMode、Windows 截图验证脚本均放在被忽略的 `Assets/LocalVerification` 和 `.local-verification`，只保留本地。已有底座的测试与工具继续保留。实际执行结果和报告位置见 `docs/validation.md`。

本机可在关闭本工程编辑器后复跑：

```powershell
& .\tools\Verify-Core.ps1
& .\.local-verification\Run-Unity.ps1 -Step Validate
& .\.local-verification\Run-Unity.ps1 -Step EditMode
& .\.local-verification\Run-Unity.ps1 -Step PlayMode
& .\.local-verification\Run-Unity.ps1 -Step Build
& .\.local-verification\Run-Player.ps1 -Visible
```

Windows 截图验证仅在 Development Build 中通过 `-algorithmSmokeTest` 启用，需要设置 `ALGORITHM_DEMO_SMOKE_OUTPUT` 为产物目录；`Run-Player.ps1` 会自动设置，并通过 `-Visible` 显示实际播放器窗口。正常运行不带自检参数。截图依赖实际图形设备和可见窗口，不能用 `-nographics` 代替；本机隐藏窗口会得到纯黑截图，自检会判失败。

## 后续开发建议

1. 新增第四个 Demo 时，先增加独立算法/控制器和场景，再更新 `DemoKind`、`DemoCatalog`、菜单接线、场景生成器及资源清单；用统一流程验证进入、重置、返回和再次进入。
2. 教学面板目前保留原 IMGUI，便于对照文章代码。做成产品工具时可迁到 GF UI/现有通用控件，增加滚动布局、移动端操作和安全区；不要把教学面板的每帧布局作为正式游戏 UI 的默认方案。
3. 本机通过系统动态字体显示中文，跨平台发布应引入有授权的字体资产。没有携带 Windows 字体文件，其他机器缺字体时需自行配置。
4. 性能优化先用 Profiler 定位邻居搜索、流场重建、Mesh 构建和 GC，再决定空间索引或 Jobs/Burst。面板 FPS、文章 benchmark 和 Null 图形测试不代表目标设备帧率。
5. `A* 对照` 和画墙后的重建都是同步计算。若正式玩法地图更大，应加入耗时预算、延迟合并重建或快照式后台计算，并在主线程应用结果。
6. 新项目继续把业务放在 Game 层，通用 UI、引导、Spine 与特效放在 Extensions；保留 GF 和其他来源的许可证。此 Demo 工程没有自动推送到 GitHub。
