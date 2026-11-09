# RTS Ability Integration Implementation Plan

> **For agentic workers:** 使用 `superpowers:executing-plans` 分步骤实施；本任务在用户当前工程中执行，测试辅助代码只保留在本机忽略目录。

**Goal:** 在 GF Demo 中加入可编译和可验证的 GameplayAbilities 技能系统，并准备 GF 资源边界。

**Architecture:** 将固定版本的第三方程序集作为工程内源码子集引入，不安装 CycloneGames 的资源提供者；新增项目适配层把 GF 的资源加载/释放转换为技能系统的资源句柄。RTS Engine 尚未取得，其单位桥接单独进行。

**Tech Stack:** Unity 2022.3、Game Framework、CycloneGames.GameplayAbilities、UniTask 2.5.0、Unity Test Framework。

**Spec:** `docs/superpowers/specs/2026-11-09-rts-abilities-design.md`

## Global Constraints

- 使用中文说明和新增代码注释；不删除文件、不提交或推送。
- 固定 CycloneGames 提交 `aea5fc8ec0adac197b17417994d6fc945458e21f` 和 UniTask `56662924960a53695a640af80125f1cfd334d5ee`，保留许可证与来源 `.meta`。
- 临时下载和验证产物位于 `E:\AI\_DownLoads\unity-gf-demos`；测试和验证脚本只留在 `Assets/LocalVerification` 或 `.local-verification`。
- 任何共享 Context 的实体在 Context 前释放；GF 的迟到资源必须释放。

### Task 1: 引入技能程序集

**Files:**
- Create: `Assets/ThirdParty/CycloneGames/`，包含 GameplayAbilities、GameplayTags、DeterministicMath、Hash 和 Logging 的必要源码及 MIT 许可证。
- Create: `Assets/ThirdParty/UniTask/`，包含 UniTask 的 Runtime、Editor 及 MIT 许可证。
- Create: `ProjectSettings/GameplayTags/Starter.json`，提供 Player 构建所需的首个标签。
- Modify: `Packages/manifest.json`，增加 `com.unity.nuget.newtonsoft-json` 版本 `3.2.2`。
- Modify: `.gitignore`，排除上游构建事务记录。
- Modify: `THIRD_PARTY_NOTICES.md`，记录固定提交、导入边界和本工程的程序集调整。

- [x] 从固定版本拷贝必要程序集及 `.meta`，跳过上游 Tests、Samples、可选资源适配和 UniTask 可选第三方集成；不得覆盖已有文件。
- [x] 按当前项目 UGUI 程序集规则配置 UniTask 引用，并通过 Unity 验证编译。
- [x] 记录第三方许可证和来源，保留 GF 原有资源配置。
- [x] 验证空标签库导致构建器无法生成数据，并添加基础标签；用无文件写入的方式验证标签数据生成。
- [x] 用 PlayMode 首场景加载前的真实标签查询复现初始化顺序问题，修正编辑器标签源恢复时机并复测。

### Task 2: GF 资源适配

**Files:**
- Create: `Assets/Starter/GameplayAbilities/Runtime/Starter.GameplayAbilities.asmdef`。
- Create: `Assets/Starter/GameplayAbilities/Runtime/GfResourceLocator.cs`。
- Create: `Assets/LocalVerification/GameplayAbilities/GameplayAbilitiesIntegrationTests.cs` 及独立测试程序集。
- Create: `Assets/LocalVerification/AbilityPlayMode/GfResourceIntegrationTests.cs` 及独立测试程序集。

**Interfaces:**
- `GfResourceLocator` 实现 `IResourceLocator.LoadAssetAsync<T>(string, string, string, string, CancellationToken)`。
- 每个成功获取的 `IResourceHandle<T>` 拥有一次 GF 引用；`Dispose` 最多释放一次。加载中取消不终止 GF，迟到成功的资产交还 GF。

- [x] 编写本地行为测试：正常加载和重复释放、加载失败、加载前取消、加载中取消后迟到成功的释放；先观察新增适配边界缺失导致测试失败。
- [x] 实现适配器，保留取消、失败和 GF 生命周期的可观察行为；单独测试各边界。
- [x] 使用 Unity EditMode 测试执行适配器测试，并在 PlayMode 通过真实 GF 场景加载资源。

### Task 3: 验证技能行为与文档

**Files:**
- Create: `Assets/LocalVerification/GameplayAbilities/GameplayAbilitiesScenarioTests.cs`。
- Create: `docs/gameplay-abilities.md`。
- Modify: `README.md`，加入技能模块入口和当前限制。

- [x] 使用真实技能系统创建两个共享 Context 的实体，验证技能消耗、冷却阻止二次触发、冷却结束和效果过期。
- [x] 文档记录 Context / ASC 所有权、GF 资源适配、标签创作入口、运行测试方法及 RTS Engine 的待接入边界。
- [x] 运行 `tools/Verify-Unity.ps1` 的相关测试与现有回归，检查退出码、XML 和编译日志；报告验证范围及所有尚未完成项。
- [x] 经用户允许清理本次构建生成的标签资源和事务临时文件，构建 Windows Development Player，并在实际 Player 中验证示例标签可读取。
