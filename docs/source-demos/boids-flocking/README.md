# Boids 鸟群 —— Unity 版

对应文章：《涌现之美：用 Cocos 手搓 3A 大作同款的“群体智能”》（https://mp.weixin.qq.com/s/khqncAqAd3_OPcYdSASLhA）
浏览器版：`../../demos/boids-flocking/`

## Unity 版本

- 目标：Unity 2022.3 LTS 与 Unity 6（6000.0）。
- 渲染：一张每帧重建的动态 Mesh + `Graphics.DrawMesh` + `Sprites/Default` 材质，Built-in / URP 通用；UI 用 IMGUI（`OnGUI`），不依赖 UGUI / TextMeshPro。
- 输入：自动适配 Active Input Handling。选 Input Manager (Old) 时走 `Input.GetMouseButton`，选 Input System 或 Both 时走 `Mouse.current`。

## 打开与运行

**方式 A：直接把本目录当工程打开**
Unity Hub → Add → Add project from disk → 选 `boids-flocking/`。目录里已有 `ProjectSettings/ProjectVersion.txt`（2022.3.62f3）和只含内置模块的 `Packages/manifest.json`，其余设置由 Unity 首次打开时生成。用别的 2022.3 / Unity 6 版本打开时，Hub 会提示切换版本，确认即可。打开后在默认的 Untitled 场景里直接按 Play。

**方式 B：拷进已有工程**
把 `Assets/BoidsFlocking/` 拷进任意工程的 `Assets/` 下，打开任意场景按 Play。

两种方式都**不需要搭场景**：`BoidsBootstrap` 用 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 在场景里没有 `BoidsDemo` 时自动创建一个，并把主相机设成正交、1 单位 = 1 像素、屏幕中心为原点（与原文 Cocos 坐标系一致）。场景里没有相机时会新建一个。想手动放置的话，把 `BoidsDemo` 挂到任意物体上即可，自动创建就不会再触发。

> 三个 Unity Demo 若拷进同一个工程，每个 Bootstrap 都会创建自己的 Demo，几个 Demo 会叠在一起。建议一个 Demo 一个工程。

## 操作

| 操作 | 效果 |
| --- | --- |
| 左上角四个按钮 | 文章阶段一～四：只会乱飞 → +分离 → +对齐 → +凝聚 |
| 滑条 | 数量（10～1500）、感知半径、分离 / 对齐 / 凝聚权重 |
| “拖尾”勾选框 | 每只鸟保留最近 8 帧的位置画渐隐尾迹，穿屏时切断（对应原文 `MotionStreak.reset`） |
| 左键按住 | 吸引鸟群（Seek × 2），对应文章阶段五 |
| 右键按住 | 300px 内四散逃离（Flee：2 倍速度、3 倍转向力） |

左下角 HUD 显示 FPS、逻辑耗时、鸟的数量，以及“方向一致度”：全体速度方向的一致程度，1 = 完全同向，接近 0 = 完全随机。

## 代码结构

| 文件 | 说明 |
| --- | --- |
| `Scripts/Boid.cs` | 单只鸟：分离 / 对齐 / 凝聚 / Seek / Flee / 积分，逐行对应原文 `Boid.ts`；纯数据类，不继承 MonoBehaviour |
| `Scripts/FlockSimulation.cs` | 原文 `FlockManager.update` 的逻辑部分：先统一算受力，再统一移动、穿屏 |
| `Scripts/BoidsDemo.cs` | MonoBehaviour：输入、拖尾、绘制、IMGUI 面板 |
| `Scripts/BoidsBootstrap.cs` | 进入 Play 时自动创建 Demo |
| `Scripts/Common/` | `DemoCamera`（像素正交相机）、`ShapeBatch`（动态网格批绘制）、`DemoInput`（新旧输入适配）、`DemoGui`（IMGUI 小工具） |
| `DotnetChecks/` | 编辑器外的编译检查与逻辑测试（Unity 不会导入这个目录） |

与原文的差异：
- 随机数用 `System.Random(seed)`，方便复现和测试。
- 原文的拖尾用 Cocos `MotionStreak`；这里用位置历史画渐隐线段，保持“一张 Mesh 画完所有东西”。
- 阶段一～四按钮会重设三个权重；权重滑条可以随时微调。

## 性能说明

所有鸟每帧都遍历全体（O(N²)），这正是下一期四叉树要解决的问题。在 .NET 8 Release 下，1000 只约 13 ms/帧。编辑器（Mono JIT）通常还会慢一些，建议数量控制在几百只；要上千只请看 `quadtree-boids`。

## 已验证 / 未验证

在 Linux 虚拟机上实际执行了 `DotnetChecks/run-checks.sh`（需要 .NET 8 SDK）：

- **编译**：用 Unity 6000.0.85f2 和 2022.3.62f3 编辑器自带的真实 `UnityEngine.*Module.dll` 编译 `Assets` 下全部脚本，C# 9 / .NET Standard 2.1，警告视为错误。旧输入、新输入系统两种配置均 0 警告 0 错误。新输入系统用的是照 com.unity.inputsystem 1.11.2 源码签名写的桩（`DotnetChecks/InputSystemStub.cs`），不是真实包。
- **逻辑**：在 .NET 8 里直接运行 `Boid` / `FlockSimulation`，用的是编辑器里真实的 `UnityEngine.CoreModule`（`Vector2` 是纯托管代码）：
  - 方向一致度：只开分离 0.09 → 加上对齐 0.68；
  - 左键吸引 3 秒：平均距离 402 → 9 px；
  - 右键驱散 2.5 秒：300px 内的鸟 100 → 5 只；
  - 速度从不超过上限，穿屏后位置始终在缓冲区内。
- **未验证**：虚拟机上能下载并启动 Unity 编辑器，但没有 Unity 账号许可证（报 "No valid Unity Editor license found"），**没能在编辑器里实际 Play**。所以画面、IMGUI 布局、URP 下的显示效果、Input System 的真实行为都没有亲眼确认。

自己验证：

```bash
cd DotnetChecks
bash run-checks.sh "/path/to/Unity/Editor" UNITY_2023_1_OR_NEWER   # Unity 6
bash run-checks.sh "/path/to/Unity/Editor"                         # Unity 2022.3
```
