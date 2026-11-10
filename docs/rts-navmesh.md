# RTS 单位接入 Unity NavMesh

本工程使用 Unity 2022.3.62f2c1 已启用的 `com.unity.modules.ai`，不依赖 RTS Engine，也没有安装新的寻路包。`Starter.Navigation` 把单位移动命令封装在独立程序集，GF 继续管理启动、场景和资源；选择单位、鼠标输入和显示属于示例层。

## 操作示例

打开 `Assets/Game/Scenes/Boot.unity` 并运行，在菜单选择 `NAVMESH`：

- 左键选择一个单位；按住 Shift 左键可以增加或取消选择。
- 右键点击地面发送移动指令，单位会绕过已烘焙的墙。黄色单位表示已选中，线条显示第一名选中单位的路径。
- 按 S 停止所选单位；顶部 `RESET` 重载场景，`< HUB` 或 Esc 返回菜单。
- 多个单位采用简单间隔目标点，并使用 NavMeshAgent 的局部避让；目标不合适时，界面会显示实际接受命令的数量。

演示使用旧 Input Manager 的鼠标和键盘操作，触屏操作尚未接入。现有 `Demos/1. Configure Algorithm Demos` 只会创建缺失的示例场景，不会自动重烘焙已有场景。

## 400 单位群体移动对比

在 `NAVMESH` 场景点击“开始 400 单位移动测试”。演示代码位于 `Assets/AlgorithmDemos/Runtime/NavMesh/NavMeshPackageStress*.cs`，随业务场景创建和释放；克隆工程即可使用，无需本地验证目录。

- “切换 NavMesh”和“切换流场”使用同一张地图与 400 个单位的初始队形，分别观察逐单位寻路和共享流场。地面采用 XZ 平面，Y 轴表示高度。
- 在地图上右键改变整队目的地。无效目标会保留原命令；刚擦开但导航尚未更新的目标会排队，更新后执行最新命令。
- 路线提供目标标记、编队预览、代表路线和全部路线四种显示。默认右键后短暂显示约 1 秒再淡出；可以切换常显、隐藏，或按住 Alt 临时查看。线路贴近地面，单位可以遮挡线路。
- 动态地图面板选择障碍物、擦除、沼泽或道路，用左键拖动绘制，支持 1、5、11 格画笔。普通地形速度为 4，沼泽约为 2，道路约为 6 个 Unity 单位每秒；切换算法保留地图，“重置地图和单位”恢复初始场景。
- 地图更新与移动命令分开处理。远处无关编辑保留现有路线和站位；新增墙体阻挡原路线、路径失效时，NavMesh 只给受影响单位重算。流场在相关路线受阻时更新共享方向场。等待通路的单位会在通路打开后重试原命令。
- 编辑期间单位继续移动。新墙尚未进入导航网格时只局部拦住可能撞墙的单位；异步构建的在途地图快照也参与保护，避免快速画墙再擦除使单位脱离网格。面板显示本批重寻路数量或共享场更新次数。

这里的动态地图、简易队形与流场分离属于演示层；可复用的单位命令组件仍位于下文的 `Starter.Navigation`。自动验收脚本保留在本地忽略目录，不随源码上传。

## 接入游戏单位

1. 为业务场景烘焙与单位类型匹配的 NavMesh，将固定地形和墙体纳入烘焙。示例场景的数据位于 `Assets/AlgorithmDemos/Scenes/NavMesh/NavMesh.asset`。
2. 单位 GameObject 添加 `NavMeshUnitMover`。组件会要求同一对象有 `NavMeshAgent`；设置 Agent Type、半径、高度、速度和 Area Mask，并确保出生位置处于对应网格。
3. 在引用自身 asmdef 的业务程序集时增加对 `Starter.Navigation` 的引用；`Starter.Game` 已经添加。业务层从玩家指令取得目标位置，再调用组件：

```csharp
using Starter.Navigation;
using UnityEngine;

public static class UnitOrders
{
    public static MoveRequestResult Move(NavMeshUnitMover mover, Vector3 target)
    {
        return mover.MoveTo(target);
    }

    public static bool Stop(NavMeshUnitMover mover)
    {
        return mover.Stop();
    }
}
```

`MoveTo` 使用 Agent 对应的类型和 Area Mask，在目标附近进行采样，再同步计算完整路径。`Accepted` 表示已接受；`AgentUnavailable` 表示组件不可用或单位未在网格上；`InvalidDestination` 表示坐标非法或附近找不到网格；`Unreachable` 表示没有完整路径；`AssignmentFailed` 表示 Unity 未接受算出的路径。无效或不可达指令不会主动清除上一个有效路径。`Stop` 会清除路径和速度，返回值表示停止指令是否能够执行。

目标采样半径默认 0.75 个 Unity 单位，可在组件 Inspector 中调整。采样会把点吸附到附近网格，并不保证楼层正确；多层地图需要业务层判断楼层和点击目标。修改地形、固定障碍或烘焙参数后，需要重新烘焙对应场景。当前示例生成器使用 Unity 2022 的旧版 Navigation Static 烘焙入口；该标记在当前编辑器已被 Unity 标记为弃用，升级 Unity 时应评估官方 AI Navigation 包及迁移流程。

## 进入商业 RTS 业务前仍需建设

- 鼠标框选、触屏选择、指令队列、不同体型和地形通行规则。
- 编队分配、大量单位同时发令时的寻路预算、拥堵和局部避让策略。当前逐单位同步算路适合演示及小规模验证，不代表大规模性能指标。
- 建筑放置、单位出生和销毁，以及攻击、采集等行为之间的移动状态切换；将群体移动演示中的动态障碍、网格更新和不可达反馈接入正式业务。
- 与动画、技能、碰撞、视野、战争迷雾和服务端权威/同步方案的结合。Unity NavMeshAgent 不是跨平台确定性模拟，不能直接作为严格帧同步一致性的保证。
- Android、WebGL 及微信/抖音小游戏的转换、触摸输入、包体与真机性能验证；现有 Unity 2022 演示不能证明 Unity 6 或这些平台已经可发布。
