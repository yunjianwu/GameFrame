# Unity GF Starter

给 Unity 新项目使用的独立 GF 源码工程，基于 Unity 2022.3 LTS、Built-in 渲染管线和 C# 业务代码。示例包含一条完整链路：**启动 → 版本/资源检查 → 配置预加载 → 进入游戏 → GF 场景切换 → 菜单/玩法 → 事件与本地保存**。默认 Editor/Package 离线运行，可更新模式有版本清单、下载确认、资源组更新与进度流程。

已迁入原工程的通用引导、Spine 扩展、粒子预算及常用 UI：虚拟列表/翻页/分组展开/行内详情、富文本、打字机、Tab、按钮缩放、形状、渐变和描边。使用 `Starter/5. Create or Open Extension Gallery` 打开五页交互展示；常用预设在 `Assets/Extensions/Samples/Prefabs`。

## 直接开始

1. 在 Unity Hub 中添加本目录 `D:\workspace\UnityGFStarter`，使用已安装的 `2022.3.62f2` 编辑器打开。
2. 打开 `Assets/Game/Scenes/Boot.unity`，点击 Play。
3. 点击 `ENTER DEMO`、`GRANT COINS`、`BACK TO MENU`，观察金币变化与界面重开。
4. 修改 `Assets/Game/Content/Config/GameConfig.json`，停止后重新运行。金币已保存时，`startingCoins` 不会覆盖存档。

工程已经带有场景和 UI 预设。`Starter/1. Create or Open Demo` 可以打开示例；生成器遇到已有资产会保留它们，避免覆盖后续业务修改。`Starter/2. Validate Demo` 检查必要资产、脚本引用与 Procedure 注册。`Starter/7. Configure Procedure Flow` 用于给早期底座补启动接线，当前工程已经配置。

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
| `Assets/Extensions/UI`、`Tutorial`、`Spine`、`Effects` | 独立程序集的通用控件、引导、动画和特效 | 按需引用；业务不要写入通用模块 |
| `Assets/Extensions/Samples` | 五页展示场景、预设、配置和生成骨架 | 从这里复制结构与接线示例 |
| `Assets/ThirdParty/Spine` | Spine 4.1 本地源码、Editor 与 Shader | 素材导出版本保持匹配，保留许可证 |
| `Assets/Starter/Tests`、`tools` | 核心回归和 Unity 场景集成验证 | 修改生命周期和基础能力时复跑 |

完整 GF 源码保留在工程中，示例只使用其中一部分。Entity、Sound、DataTable、Localization、Network 等模块可以继续接入；保留源码不代表这些模块已经完成项目业务、配置和平台验收。

## 建议先读

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

默认构建、日志和测试输出到 `E:\AI\_DownLoads\unity-gf-starter`。本机 `Library` 和 `Temp` 是指向该目录的目录联接。备份/移植工程时只复制 `Assets`、`Packages`、`ProjectSettings`、`docs`、`tools` 和根目录说明文件，不要沿着缓存联接复制缓存。换电脑后的缓存处理见快速接入文档。
