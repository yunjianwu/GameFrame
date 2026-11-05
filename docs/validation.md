# 三 Demo 整合验证记录

验证日期：2026-11-05。工程：`D:\WorkSpace\UnityGFDemos`。这里只记录实际执行结果；原 UnityGFStarter 的历史记录保留在 `validation-framework-baseline.md`。

## 环境

- Windows x64，Unity 2022.3.62f2c1；安装目录为 `C:\Program Files\Unity\Hub\Editor\2022.3.62f2`。
- Built-in 渲染管线，Windows x86_64 / Mono / Development Build。
- 实际图形运行使用 Direct3D 11、NVIDIA GeForce RTX 3050、1280×720 可见窗口。
- 核心回归使用 .NET 10 SDK。编辑器逻辑测试使用 `-batchmode -nographics`，画面验收另外运行真实图形播放器。

## 已执行结果

| 检查 | 结果 | 覆盖内容 |
|---|---|---|
| 核心 C# 回归 | 30 passed / 0 failed；编译 0 警告、0 错误 | 队列线程约束、事件作用域、金币、GZip、资源启动与失败/取消 |
| Unity 导入、Configure | 成功 | 三套算法编译，菜单/导航、三个场景、流程注册、资源映射、横屏设置 |
| Unity EditMode | 41 passed / 0 failed / 0 skipped | 原 35 项与新增 6 项算法检查 |
| Unity PlayMode | 29 passed / 0 failed / 0 skipped | 原 28 项与新增三个 Demo 的完整切换、重置、回收流程 |
| 构建前资产验证 | 成功 | 底座、扩展示例及三个算法场景，无 Missing Script，组件/资源/Procedure 接线有效 |
| GF Windows64 Package | 成功 | 配置、池化 UI、Menu/Play 与三个算法场景均生成真实资源包 |
| Windows 独立播放器 | 构建成功；图形自检退出码 0 | 实际从 GF Package 加载三个场景、重置与返回 |
| 实际画面 | 6 张 1280×720 PNG 已逐张查看 | 选择页、Boids、流场人群/向量、四叉树实验室/鸟群；中文可读，导航与面板没有明显重叠或截断 |
| 来源保护 | 2,253 + 51 个来源文件对照 SHA256，0 差异 | 原 UnityGFStarter 和三个原 Demo 的已记录来源文件保持不变 |

新增算法检查包括：200 个重合点的四叉树终止/完整查询、随机区域查询与暴力结果对照、四叉树和暴力鸟群位置对照、默认流场与 A* 代价对照、画墙后的改道/墙上零向量，以及鸟群吸引/速度上限/有限坐标。

新增 PlayMode 从 Boot 开始，验证菜单不自动启动模拟、重复点入口仅加载一份、旧业务场景卸载且 Boot 常驻、Demo 自带相机、导航区域拦截、RESET 真正销毁旧根并重载、返回后清空静态注册和输入区域、动态 Mesh 被销毁，以及池化菜单再次进入时仍使用当前流程回调。另检查默认零深度 UI 组在首次使用时已为屏幕叠加模式。

Windows 图形自检会等待 Unity 启动屏结束，对纯色截图判失败；分别进入三套场景，切换流场向量阶段和四叉树鸟群模式，每个场景执行重置和返回，并断言实际使用 `ResourceMode.Package`。这里的 UI 导航由本地自检调用真实按钮回调，未把这次运行描述为完整人工鼠标、键盘与触摸测试。

## 发现并处理的问题

1. **零深度 UI 组未初始化。** GF 在新 UI 组深度已经为 0 时跳过 `SetDepth`；原底座只在该方法设置 Canvas，导致默认组停留在世界坐标。新增导航输入断言先失败，诊断得到 Canvas 为 WorldSpace、面板坐标位于原点附近；新工程的 UguiGroupHelper 在 Awake 初始化 Canvas 后，完整 PlayMode 的 29 项通过。原框架来源未改写。
2. **四叉树重合点持续细分。** 本工程副本增加最大深度 16、极小节点停止分裂与容量检查，仍保留终止节点内的点；重合点与随机查询检查已通过。
3. **隐藏窗口不提交画面。** 第一轮播放器逻辑成功但截图全黑，不能作为视觉通过证据。等待启动屏后仍复现；增加纯色截图失败检查，再以可见窗口运行，同一构建生成 6 张不同的有效画面并通过。失败日志保留，没有用逻辑标记替代画面检查。
4. **首轮 EditMode 退出阶段挂起。** 首轮编辑器在写出完整 Passed XML 后未退出；本地脚本等待 20 秒后仅回收它自己启动的 runner，再独立校验结果 XML。没有结果、结果为空或任何失败均会判失败。最后一轮 EditMode 的 41 项通过且正常退出，PlayMode、Configure 和 Windows 构建也正常退出。

## 报告与产物

根目录：`E:\AI\_DownLoads\unity-gf-demos`。

```text
build/verification/20261105-151831-configure/Unity.log
build/verification/20261105-153421-editmode/EditMode.xml
build/verification/20261105-152542-playmode/PlayMode.xml
build/verification/20261105-153003-build/Unity.log
build/player-validation/20261105-153249/player.log
build/player-validation/20261105-153249/screenshots/
build/packages/20261105-153017-6eb8f2af/player/UnityGFDemos.exe
build/migration/framework-source-baseline.json
build/migration/demo-source-baseline.json
```

图形自检成功标记：`ALGORITHM_PLAYER_SMOKE_PASSED:`。配置与构建标记：`ALGORITHM_DEMOS_CONFIGURED:`、`ALGORITHM_DEMOS_VALIDATED:`、`STARTER_PLAYER_BUILT:`。播放器日志中没有本轮自检收到的 Error、Exception 或 Assert。

失败的首轮 PlayMode 与隐藏窗口截图记录也保留在同一产物根目录下。新增测试与验证辅助脚本只留本地，位于忽略目录 `Assets/LocalVerification`、`.local-verification`。

## 复跑与边界

关闭本工程的 Unity 编辑器后，在工程根目录运行：

```powershell
& .\tools\Verify-Core.ps1
& .\.local-verification\Run-Unity.ps1 -Step Validate
& .\.local-verification\Run-Unity.ps1 -Step EditMode
& .\.local-verification\Run-Unity.ps1 -Step PlayMode
& .\.local-verification\Run-Unity.ps1 -Step Build
& .\.local-verification\Run-Player.ps1 -Visible
```

普通双击 `UnityGFDemos.exe` 不带自检参数，启动后停留在菜单，可交互体验。`Run-Player.ps1` 用自检参数启动，结束后自动退出。构建和验证脚本不会关闭用户原来打开的其他 Unity 工程。

尚未验收 Android / iOS 真机、IL2CPP、微信/抖音小游戏、浏览器 WebGL、触摸、多分辨率/安全区和长时间压力。没有针对 10,000 只鸟给出帧率保证；面板瞬时 FPS 与原文章历史 benchmark 不代表目标设备性能。A* 对照为同步主线程计算，点击可能暂时卡顿。

中文面板使用本机系统动态字体，未复制 Windows 字体；其他设备需要有授权的字体资产。CDN 热更新、商业 Spine 素材、线上服务及原项目加密资源仍沿用底座记录中的未验收边界。
