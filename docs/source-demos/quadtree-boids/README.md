# 四叉树 QuadTree × Boids —— Unity 版

对应文章：《打破O(N²)性能魔咒：用四叉树(QuadTree)让同屏万只鸟群满帧运行！》（https://mp.weixin.qq.com/s/iyya7FYjmSE1AvRnhTRxPA）
浏览器版：`../../demos/quadtree-boids/`

## Unity 版本

- 目标：Unity 2022.3 LTS 与 Unity 6（6000.0）。
- 渲染：动态 Mesh + `Graphics.DrawMesh` + `Sprites/Default`，Built-in / URP 通用；UI 用 IMGUI。
- 输入：自动适配旧 Input Manager / 新 Input System。

## 打开与运行

**方式 A：直接把本目录当工程打开**
Unity Hub → Add project from disk → 选 `quadtree-boids/`。打开后在默认场景里直接按 Play。

**方式 B：拷进已有工程**
把 `Assets/QuadTreeBoids/` 拷进任意工程的 `Assets/`，打开任意场景按 Play。

不需要搭场景：`QuadTreeBootstrap` 会自动创建 `QuadTreeDemo`，并把主相机设成 1 单位 = 1 像素的正交相机。建议一个 Demo 一个工程，原因见 `boids-flocking/README.md`。

## 操作

左上角页签切换两个模式。

**四叉树实验室**（文章阶段二 `QuadTreeLab`）

| 操作 | 效果 |
| --- | --- |
| 左键点空白处 | 加点，格子超过容量就一分为四 |
| 按住已有的点拖动 | 移动点，实时观察分裂与合并 |
| 鼠标悬停 | 点变黄；鼠标处的橙框是实时范围查询，命中的点变橙（本 Demo 附加，用来直观展示 `Query`） |
| 滑条 / 按钮 | 节点容量（1～10）、查询半边长；“重置为 10 个随机点” |

**万鸟压测**（文章阶段三 + 性能实测）

| 操作 | 效果 |
| --- | --- |
| “启用四叉树”勾选框 | 关掉就回到 O(N²) 暴力遍历，对比 HUD 里的逻辑毫秒数 |
| 滑条 | 鸟的数量（100～10000）、节点容量（1～32）、感知半径 |
| “绘制树网格” | 画出本帧的四叉树 |
| 左键 / 右键按住 | 吸引 / 驱散 |

## 代码结构

| 文件 | 说明 |
| --- | --- |
| `Scripts/QuadTree.cs` | `Rectangle`、`QuadPoint<T>`、`QuadTree<T>`：Insert / Subdivide / Query，对应原文 `QuadTree.ts` |
| `Scripts/FlockSimulation.cs` | `UseQuadTree` 开关：每帧重建树，每只鸟以自身为中心、半边长为感知半径查询邻居，再交给 `Boid.Flock` |
| `Scripts/Boid.cs` | 与 `boids-flocking` 相同 |
| `Scripts/QuadTreeDemo.cs` | 实验室 + 压测两个模式的输入、绘制、IMGUI |
| `Scripts/QuadTreeBootstrap.cs`、`Scripts/Common/` | 自动创建、相机、批绘制、输入适配、IMGUI 工具 |
| `DotnetChecks/` | 编辑器外的编译检查、逻辑测试与压测 |

与原文的差异：
- 原文每帧 `new` 一棵新树；这里 `Reset` 后复用节点对象，分裂逻辑完全相同，只是避免每帧上千个节点的 GC 分配。
- 原文的树存的是 `Point`（`userData: any`），这里改成泛型 `QuadPoint<T>`，实验室用 `int` 下标、鸟群用 `Boid` 引用。
- 四叉树边界取“屏幕一半 + 50px”，与穿屏缓冲区一致，否则缓冲区里的鸟插不进树。
- 已知限制（与原文一致，未改）：同一坐标上的点多于容量个时会无限分裂。

## 性能说明

.NET 8 Release 下纯逻辑每帧耗时（1280×720，感知半径 50，30 帧平均）：

| 数量 | 暴力遍历 | 四叉树 | 加速比 |
| --- | --- | --- | --- |
| 500 | 3.3 ms | 0.6 ms | 5.5x |
| 1000 | 12.6 ms | 1.7 ms | 7.6x |
| 2500 | 78 ms | 6.8 ms | 11.5x |
| 5000 | 307 ms | 22.7 ms | 13.5x |
| 10000 | 1232 ms | 77 ms | 16.0x |

- 2500 只的量级与文章一致（42.67 → 12.02 ms）。1 万只时四叉树也要约 77 ms/帧，**达不到标题说的“万只满帧”**。
- 编辑器里是 Mono JIT，通常比 .NET 8 慢；IL2CPP 打包后会快一些。
- 要真正满帧上万只，可以继续：把三次邻居遍历合成一次、换成均匀网格分桶、用 Jobs + Burst 并行。本 Demo 没做这些优化，保持与文章一致。

## 已验证 / 未验证

在 Linux 虚拟机上实际执行了 `DotnetChecks/run-checks.sh`：

- **编译**：对 Unity 6000.0.85f2 与 2022.3.62f3 编辑器自带的真实 `UnityEngine.*Module.dll` 编译全部脚本，旧输入 / 新输入系统（桩）两种配置均 0 警告 0 错误。
- **逻辑**：用真实的 `UnityEngine.CoreModule` 在 .NET 8 中运行：
  - 2000 个点、500 次随机范围查询，四叉树结果与暴力筛选完全一致；
  - 12 个密集点触发多级分裂，复用节点与新建结果一致；
  - 同一批 800 只鸟分别用暴力遍历和四叉树跑 10 帧，最大位置差 9.8e-5 px（float32 求和顺序差异）；
  - 上表压测。
- **未验证**：VM 上没有 Unity 许可证，**没能在编辑器里实际 Play**，画面、IMGUI 布局、编辑器内帧率都未亲眼确认。

```bash
cd DotnetChecks
bash run-checks.sh "/path/to/Unity/Editor" UNITY_2023_1_OR_NEWER   # Unity 6
bash run-checks.sh "/path/to/Unity/Editor"                         # Unity 2022.3
```
