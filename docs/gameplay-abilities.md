# 技能模块接入说明

工程已引入 CycloneGames.GameplayAbilities、GameplayTags 和所需程序集。它提供技能授予与激活、消耗、冷却、属性、持续效果和状态标签；项目可以继续使用 GF 处理启动、场景、UI 和资源加载。

RTS Engine 尚未取得，本工程没有 RTS Engine 单位适配器，也没有自动生成 RTS 玩法。它可通过 [Unity Asset Store 官方页面](https://assetstore.unity.com/packages/tools/game-toolkits/rts-engine-79732) 使用拥有该资产的 Unity 账号获取。取得合法资产后再依据实际版本的 API 接入单位、移动、攻击和死亡。

## 目录与创建入口

| 位置 | 用途 |
|---|---|
| `Assets/ThirdParty/CycloneGames` | 固定版本的技能、标签、定点数、哈希和日志契约源码 |
| `Assets/ThirdParty/UniTask` | 技能异步操作所需的 UniTask |
| `Assets/Starter/GameplayAbilities/Runtime` | GF 资源加载适配器 |
| `ProjectSettings/GameplayTags/Starter.json` | 首个示例标签，项目可继续在此目录创建标签文件 |
| `Tools/CycloneGames/GameplayTags/Gameplay Tag Manager` | 标签编辑器 |
| `Tools/CycloneGames/GameplayAbilities/Debugger` | 运行时技能状态调试器 |
| `Assets/Create/CycloneGames/GameplayAbilities/Definitions/Gameplay Effect` | 创建效果配置资产 |

`State.CrowdControl.Stunned` 只是示例标签；是否禁止施法和移动仍需业务规则显式判断。上游完整教程与 API 说明可从 `Assets/ThirdParty/CycloneGames/CycloneGames.GameplayAbilities/README.SCH.md` 查看；其中 AssetManagement、DataTable、网络和 Samples 的安装指引不适用于本次导入的源码子集。

编辑器进入 PlayMode 时会在首个场景加载前恢复项目标签源，使场景启动代码可以读取 `ProjectSettings/GameplayTags` 中的标签。Player 使用构建阶段生成的标签数据；Windows 开发版已验证能在运行时查询示例标签。

## 对局和实体生命周期

技能系统不会在现有 Boot 或三个算法 Demo 中自动创建实体。新的 RTS 对局应在 GF 已绑定资源组件后建立一个对局级 `GASRuntimeContext`，让需要互相施加技能效果的单位共用它。每个需要技能的单位持有自己的 `AbilitySystemComponent`，由对局模拟按约定时间步调用 `Tick`。不要为每个需要交互的单位分别创建独立 Context，否则跨 Context 施加效果会被拒绝。

以下片段展示需要由对局管理器持有的对象和调用顺序，`casterObject` 和 `targetObject` 代表将来的实际单位：

```csharp
using CycloneGames.GameplayAbilities.Runtime;
using Starter.GameplayAbilities;
using Starter.Runtime;

// 在 GF 初始化完成后、Unity 主线程上创建。
var poolConfig = new GameObjectPoolManager.PoolConfig(
    maxAssetPools: 32,
    maxActiveLeases: 256,
    maxActiveLeasesPerPool: 64,
    maxRetainedInstancesPerPool: 32,
    minRetainedInstancesPerPool: 0,
    idleExpirationTime: 60f,
    maxTotalRetainedInstances: 256);
var cues = new GameplayCueManager(poolConfig);
cues.Initialize(new GfResourceLocator(GameServices.Resource));
var world = new GASRuntimeContext(cueManager: cues);

var caster = new AbilitySystemComponent(world);
caster.InitAbilityActorInfo(casterObject, casterObject);
var target = new AbilitySystemComponent(world);
target.InitAbilityActorInfo(targetObject, targetObject);

// 对局更新时：由同一个模拟时间源分别驱动所有存活实体。
caster.Tick(deltaTime, isServer: true);
target.Tick(deltaTime, isServer: true);

// 结束对局：停止新请求，先释放全部 ASC，再释放 Context 和 Cue 服务。
target.Dispose();
caster.Dispose();
world.Dispose();
cues.Dispose();
```

示例 `isServer: true` 表示本地权威模拟，不代表已经实现网络同步。服务端或不需要技能表现的模拟可以使用默认的空 Cue 服务，无需创建 `GameplayCueManager`。池容量只是示例，实际规模必须结合目标设备与同屏数量测量；上游 Cue 管理器内部使用自己的 GameObject 池，GF 适配器负责底层资源加载与卸载。

## GF 资源适配

`GfResourceLocator` 接收 `ResourceComponent`，以 `IResourceLocator` 接口供技能表现使用。资源路径必须是 GF 使用的完整路径，例如 `Assets/Game/Content/...`，并且需要按项目 GF 资源集合正常配置。加载成功后，技能系统持有的句柄负责释放 GF 引用；重复释放不会重复卸载。

GF 没有通用的取消加载句柄。请求取消会结束等待，但底层加载仍可能完成；迟到的资源会在 GF 回调时释放。请在 GF 资源服务关闭前停止技能表现请求并让已提交的回调完成。适配器及其句柄须在创建它的 Unity 主线程使用；从其他线程触发的取消会投递回创建线程。`bucket`、`cacheTag`、`cacheOwner` 当前不映射到 GF 分组策略。

上游日志契约默认使用空 writer。若项目需要技能诊断，应在组合入口实现 `CycloneGames.Logging.ILogWriter` 并通过 `LogRuntime.TryInstallWriter` 显式接入已有日志；当前并未自动安装日志后端。

## Player 构建注意事项

上游 GameplayTags 构建钩子要求至少存在一个正式标签；本工程提供 `Starter.json` 作为起点。构建时上游会在 `Assets/Generated/CycloneGames.GameplayTags` 下临时生成标签数据，完成后清理；事务状态位于已忽略的 `.buildpipeline/transactions/gameplay-tags`，正常完成后会保留空的 `build.lock`。这两个内部路径由上游固定，尚未改造成全部写入 `E:\AI\_DownLoads`。构建中断时先检查事务状态及上游文档中的恢复流程，不要直接删除未知文件。

2026-11-09 已构建 Windows x86_64 Development Player，并在真实 Player 内成功查询 `State.CrowdControl.Stunned`，专项检查正常退出。该检查没有覆盖 Player 内的完整技能战斗或其他平台。另一次隐藏窗口的完整图形冒烟测试在主菜单截图阶段读到纯色，未完成三场景截图验证。

## RTS 桥接约定

取得 RTS Engine 后，以其单位系统为生命值、死亡、普通攻击等状态的权威来源。技能消耗和冷却由 GameplayAbilities 管理，伤害和治疗通过适配器调用 RTS Engine 的实际接口；不要同时维护两套权威血量。普通单位受减速、眩晕等效果影响的方式、同步边界以及大量单位的性能需要结合所取得版本单独设计和测试。

本机的技能适配和场景验证位于 Git 忽略目录 `Assets/LocalVerification`。可以关闭本工程的 Unity 编辑器后运行：

```powershell
& .\tools\Verify-Unity.ps1 -Mode EditMode -TestFilter 'Starter.AbilityIntegration.Tests'
& .\tools\Verify-Unity.ps1 -Mode PlayMode -TestFilter 'Starter.AbilityIntegration.Tests.GfResourceIntegrationTests'
& .\.local-verification\Run-Unity.ps1 -Step Build
& .\.local-verification\Run-Player.ps1 -TagsOnly
```

这些本地测试不随项目提交；报告写入 `E:\AI\_DownLoads\unity-gf-demos\build\verification`。
