# 快速接入

## 打开并运行

工程根目录是包含 `Assets`、`Packages`、`ProjectSettings` 的 `D:\workspace\UnityGFStarter`。用 Unity 2022.3 LTS 打开，建议先使用此次实际验证的安装路径：

```text
C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe
```

该安装包运行时报告 `2022.3.62f2c1`，Unity 导入后也将这个实际版本写入了 `ProjectVersion.txt`。Hub 安装目录名称是 `2022.3.62f2`。不要直接用 Unity 6 升级后回写唯一副本；先在新目录验证编辑器迁移。

打开 `Assets/Game/Scenes/Boot.unity`，Play 后先出现菜单。点击进入示例，点击加金币，再返回和重开。`SAVE LOCALLY` 会写入本地设置；暂停和退出也会保存。GF Setting 默认使用 PlayerPrefs，这适合小规模设置和演示存档；正式游戏的大存档应单独设计版本、校验、原子写入和恢复策略。

示例 UI 使用内置 `LegacyRuntime.ttf` 和英文文本，避免首次运行依赖外部字体。项目说明与关键代码备注使用中文。正式中文界面请引入有授权的中文字库，并评估 TextMeshPro、字体图集与小游戏包体。

## 最先修改的位置

1. `Assets/Game/Content/Config/GameConfig.json`：标题、初始金币、奖励和目标帧率。已有金币存档时只改变初始值不会重置余额。
2. `Assets/Game/Runtime/GameSession.cs`：组合自己的业务服务。
3. `Assets/Game/Runtime/Procedures`：替换 Menu / Play，沿用 Launch → CheckVersion → UpdateResource → Preload → EnterGame → ChangeScene。详细位置见 `procedures.md`。
4. `Assets/Game/Content/UI` 与 `Assets/Game/Runtime/UI`：替换界面预设和脚本，保留开关订阅规则。
5. Player Settings：修改公司名、产品名、包名、屏幕方向和图标。默认包名是 `com.yourstudio.gf.starter`，不能拿它作为正式发行标识。

示例金币键为 `starter.demo.coins.v1`。开发时需要新存档可更换这个键；不要清空其他项目的 PlayerPrefs 来重置演示数据。`Core/Wallet.cs` 也是示例模型，可以移入自己的业务模块。

## 资源开发模式与 Package 模式

Boot 场景中 GF 的 Base 组件启用 Editor Resource Mode，编辑器可直接从 AssetDatabase 加载资源，不需要每次打资源包。Player 不支持 AssetDatabase，会进入 GF Package 模式，先初始化版本清单，再加载配置和界面。

资源分包定义位于 `Assets/GameFramework/Configs/ResourceCollection.xml`：

| 包名 | 资产 |
|---|---|
| `demo/config` | GameConfig.json |
| `demo/ui` | MenuForm.prefab、PlayForm.prefab |
| `demo/scenes/menu` | Menu.unity 业务场景 |
| `demo/scenes/play` | Play.unity 业务场景 |

资源路径集中在 `Assets/Game/Runtime/AssetNames.cs`。新增界面要同时维护路径常量、UI 预设和资源分包。只改常量但没把资源纳入分包，会出现编辑器成功、Player 找不到资源的问题。

`Starter/3. Build Package for Active Target` 使用官方 GF 构建器，支持当前选择的 Windows64、Android、WebGL 映射。它每次创建一个新的带时间与随机后缀的输出目录，并在调用前确认 GF 的清理目标不存在。不会复用旧版本输出。没有安装目标平台模块时，需先在 Unity Hub 配置相应模块。

资源输出结构类似：

```text
E:/AI/_DownLoads/unity-gf-starter/build/packages/<run>/resources/
  Package/0_1_0_1/Windows64/
    GameFrameworkVersion.dat
    demo/config.dat
    demo/ui.dat
    demo/scenes/menu.dat
    demo/scenes/play.dat
  Working/
  BuildReport/
```

`GameFrameworkVersion.dat` 和 `.dat` 资源文件必须在同一套构建中产生，并和 Player 的平台匹配。这里的 Package 是应用内资源，不是 CDN 热更新版本。

## 构建可运行的 Windows 示例

在 Build Settings 选择 PC, Mac & Linux Standalone → Windows → x86_64，然后用 `Starter/4. Build Windows Demo Player`。

该入口先构建 Package，再通过 Unity 的 `BuildPlayerProcessor` 把外部资源放入 Player 的 StreamingAssets 根目录。这样不用在工程 Assets 下创建资源构建产物。默认输出位于上述 `<run>/player/UnityGFStarter.exe`。

也可通过命令行调用同一个入口，日志请指定到外部目录。示例中的目录须事先存在：

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -buildTarget Win64 `
    -projectPath 'D:\workspace\UnityGFStarter' `
    -executeMethod Starter.Editor.StarterBuildTools.BuildWindowsPlayer `
    -logFile 'E:\AI\_DownLoads\unity-gf-starter\build\logs\manual-player-build.log'
```

构建日志中的 `STARTER_PLAYER_BUILT:` 后面是实际 exe 路径。开发包可传 `-starterSmokeTest` 检查真实 Package 加载、启动与金币事件，检查完成后会自动退出。普通启动不会运行自检；非 Development Build 不编译这段代码。

Android / WebGL 目前只提供资源平台映射，没有完整的平台 Player 构建、签名、小游戏转换或设备验收。先完成平台配置与真机验证，再扩展相同的资源注入流程。不要仅执行 File → Build，就假定资源已经被自动打入：外部资源注入只在本示例构建入口调用期间生效。

## 复制为自己的新项目

在新目录复制 `Assets`、`Packages`、`ProjectSettings`、`docs`、`tools`、`.gitignore`、`README.md`、`THIRD_PARTY_NOTICES.md`、`upstream.json`。保留所有 `.meta` 文件，GF prefab 和已生成场景依靠 GUID 关联。

不要复制 `Library`、`Temp`、`Logs`、`UserSettings` 或外部构建产物。这些是当前机器的缓存与运行状态；Git 忽略规则已经排除它们。按团队的磁盘规则在打开 Unity 前配置新缓存目录，或让 Unity 在新项目下重新生成缓存。本机为了遵守产物目录约定，使用了指向 E 盘的目录联接。

可用 `STARTER_BUILD_ROOT` 进程环境变量修改资源与 Player 输出根目录，默认仍为 E 盘。`Verify-Core.ps1`、`Verify-Unity.ps1` 分别支持 `-OutputRoot`，Unity 路径通过 `-UnityPath` 指定。测试输出应按新项目改为独立子目录，避免多个项目混在一起。
