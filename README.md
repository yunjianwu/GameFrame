# Unity GF Algorithm Demos

这是用之前导出的 **UnityGFStarter / GameFrame** 底座整合的独立 Demo 工程，位置为 `D:\WorkSpace\UnityGFDemos`。三套文章示例统一从 GF 菜单进入，由 Procedure、GF Scene 和池化 UI 管理启动、重置与返回。

| Demo | 可以体验的内容 | 默认规模 |
|---|---|---|
| Boids 鸟群 | 从随机运动逐步加入分离、对齐、凝聚；鼠标吸引/驱散、拖尾与调参 | 200 只 |
| Flow Field 流场寻路 | 代价场、积分场、向量场、共享流场与群体分离；画墙、沼泽、移动终点和 A* 对照 | 2,000 个体 |
| QuadTree 四叉树 | 增点/拖点、区域查询、节点分裂可视化；四叉树与暴力邻居搜索切换 | 鸟群模式 1,000 只 |

完整操作、接入位置、原文件映射与维护建议见 [三个 Demo 的接入说明](docs/algorithm-demos.md)。原来的金币示例保留在菜单右上角 `FRAMEWORK SAMPLE`；引导、Spine 和常用 UI 的五页展示也继续保留。

## GitHub 分支与维护

- [main：纯框架底座](https://github.com/yunjianwu/GameFrame/tree/main)，继续作为新游戏的起步工程。
- [demos/algorithm-lab：三个算法 Demo 的完整独立工程](https://github.com/yunjianwu/GameFrame/tree/demos/algorithm-lab)，包含框架、示例场景、Procedure、菜单与通用扩展。

在新电脑或尚不存在 `UnityGFDemos` 的目录中下载 Demo 分支：

```powershell
git clone --branch demos/algorithm-lab https://github.com/yunjianwu/GameFrame.git UnityGFDemos
```

用 Unity Hub 打开克隆得到的工程根目录，运行 `Assets/Game/Scenes/Boot.unity`。文档中的 D 盘路径是本机位置，克隆后以自己的实际目录为准。直接下载 GitHub ZIP 时也要先切到 `demos/algorithm-lab` 分支。

后续在本工程维护 Demo，先检查 `git status --short`，工作区干净时使用 `git pull --ff-only` 同步。提交前逐项核对变动和 `.meta`，只添加本次需要的源码、场景和配置；验证后提交，再用 `git push origin demos/algorithm-lab` 上传。新增测试和本地验证辅助代码继续留在忽略目录中。

两条分支共享框架历史，但各自维护：通用框架修复应单独提交，再按需用 `git cherry-pick <commit>` 同步；算法和 Demo 业务保留在 Demo 分支。升级 GF 或 Spine 时核对固定版本、许可证及回归记录。

给 Unity 新项目使用的独立 GF 源码工程，基于 Unity 2022.3 LTS、Built-in 渲染管线和 C# 业务代码。示例包含一条完整链路：**启动 → 版本/资源检查 → 配置预加载 → 进入游戏 → GF 场景切换 → 菜单/玩法 → 事件与本地保存**。默认 Editor/Package 离线运行，可更新模式有版本清单、下载确认、资源组更新与进度流程。

已迁入原工程的通用引导、Spine 扩展、粒子预算及常用 UI：虚拟列表/翻页/分组展开/行内详情、富文本、打字机、Tab、按钮缩放、形状、渐变和描边。使用 `Starter/5. Create or Open Extension Gallery` 打开五页交互展示；常用预设在 `Assets/Extensions/Samples/Prefabs`。

## 直接开始

1. 在 Unity Hub 中添加本目录 `D:\WorkSpace\UnityGFDemos`，使用已安装的 `2022.3.62f2` 编辑器打开。
2. 打开 `Assets/Game/Scenes/Boot.unity`，点击 Play。
3. 在 `ALGORITHM LAB` 中选择三个 Demo。顶部 `< HUB` 返回，`RESET` 重新加载当前场景，`Esc` 也可返回。
4. 演示结束后可进入右上角 `FRAMEWORK SAMPLE`，体验原来的金币、事件与存档示例。

工程带有已配置的场景和 UI 预设。`Starter/1. Create or Open Demo` 可打开 Boot；`Demos/2. Validate Algorithm Demos` 检查三套场景、模拟组件、导航、资源映射与 Procedure 注册。`Demos/1. Configure Algorithm Demos` 用于补齐缺失资产；已有且已接线的菜单、导航和 Demo 场景会保留，重复配置仍会应用工程参数、流程注册和横屏适配。

## 目录

| 目录 | 负责什么 | 新项目怎么处理 |
|---|---|---|
| `Assets/Framework/GameFramework` | 纯 C# GF 核心，包括任务池、资源、流程、事件、网络等 | 保留上游实现与许可证 |
| `Assets/Framework/UnityGameFramework` | Unity 组件、辅助器、资源编辑器与构建器 | 少改；优先实现自己的 Helper |
| `Assets/Starter/Core` | 主线程队列、订阅作用域、示例数据模型 | 队列和作用域可复用；Wallet 按业务替换 |
| `Assets/Starter/Runtime` | 入口、组件绑定、UI 生命周期和分层 | 项目通用规则放这里 |
| `Assets/Starter/Editor` | 示例资产生成、验证、外部资源与 Windows 开发包构建 | 按发布需求逐步扩展 |
| `Assets/Game/Runtime` | 配置、流程、金币业务与两个示例界面 | 从这里开始写自己的游戏 |
| `Assets/Game/Content` | 通过 GF 加载的配置与 UI 预设 | 新增资产后更新资源分包配置 |
| `Assets/AlgorithmDemos/Runtime` | 三套算法、演示控制、场景注册与输入拦截 | 算法保留独立命名空间；GF 接入放在 Game 层 |
| `Assets/AlgorithmDemos/Scenes` | Boids、FlowField、QuadTree 三个业务场景 | 从 Boot 菜单进入；通过 GF 加载和卸载 |
| `Assets/Extensions/UI`、`Tutorial`、`Spine`、`Effects` | 独立程序集的通用控件、引导、动画和特效 | 按需引用；业务不要写入通用模块 |
| `Assets/Extensions/Samples` | 五页展示场景、预设、配置和生成骨架 | 从这里复制结构与接线示例 |
| `Assets/ThirdParty/Spine` | Spine 4.1 本地源码、Editor 与 Shader | 素材导出版本保持匹配，保留许可证 |
| `Assets/Starter/Tests`、`tools` | 核心回归和 Unity 场景集成验证 | 修改生命周期和基础能力时复跑 |

完整 GF 源码保留在工程中，示例只使用其中一部分。Entity、Sound、DataTable、Localization、Network 等模块可以继续接入；保留源码不代表这些模块已经完成项目业务、配置和平台验收。

## 建议先读

- [三个 Demo 的操作、GF 接入与维护建议](docs/algorithm-demos.md)
- [快速接入与资源构建](docs/quickstart.md)
- [架构取舍与开发建议](docs/architecture.md)
- [Procedure 全链、场景切换与热更新接入](docs/procedures.md)
- [协程、多线程和回调边界](docs/threading.md)
- [增加界面、事件、流程和平台能力](docs/extension-guide.md)
- [引导、Spine、粒子与常用 UI 的接入和注意事项](docs/extensions.md)
- [原脚本迁移映射与改造说明](docs/migration-map.md)
- [验证记录与适用边界](docs/validation.md)
- [上游版本和许可证](THIRD_PARTY_NOTICES.md)

这套底座没有绑定原游戏的 Lua、广告、登录、支付、微信/抖音 SDK、加密资源格式和服务器地址。需要这些能力时，在项目层逐个接入。GF 官方源码固定在配套的 `v2021.05.31` 版本，具体提交见 `upstream.json`；这是一套起步底座，后续应维护自己的版本和验证记录。

## 验证与构建

```powershell
# 无需 Unity 的核心回归；需要 .NET 10 SDK，本机已安装。
& .\tools\Verify-Core.ps1

# 请先关闭本工程的 Unity 编辑器，避免项目锁冲突。
& .\tools\Verify-Unity.ps1
```

Unity 菜单 `Starter/3. Build Package for Active Target` 构建当前平台的 GF Package 资源。`Starter/4. Build Windows Demo Player` 构建带资源的 Windows 开发包；需先选 Windows x86_64。

`Starter/6. Validate Extensions` 检查扩展示例配置、骨架和预设；Verify-Unity 会同时验证基础与扩展示例并运行回归测试。

默认构建、日志和测试输出到 `E:\AI\_DownLoads\unity-gf-demos`，Windows 可执行文件名为 `UnityGFDemos.exe`。本机 `Library`、`Temp`、`Logs` 是指向该产物目录的目录联接。备份/移植工程时只复制 `Assets`、`Packages`、`ProjectSettings`、`docs`、`tools` 和根目录说明文件，保留 `.meta`，跳过缓存联接。本轮新增的 `Assets/LocalVerification` 与 `.local-verification` 只留本地，已经加入 `.gitignore`，后续提交时仍须逐个核对文件列表。换电脑后的缓存处理见快速接入文档。
