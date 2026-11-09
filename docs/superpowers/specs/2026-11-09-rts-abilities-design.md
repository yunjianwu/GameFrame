# RTS 技能模块接入设计

## 目标与边界

在现有 Unity 2022.3 工程中接入 CycloneGames.GameplayAbilities，为英雄技能提供技能授予、消耗、冷却、效果与状态标签。Game Framework 继续承担场景、UI 和资源加载等通用服务。当前未取得 RTS Engine；不能声明其已安装或已经完成桥接。

## 依赖与来源

- CycloneGames 固定到 UnityStarter 提交 `aea5fc8ec0adac197b17417994d6fc945458e21f`，保留 MIT 许可证、来源及导入的 `.meta`。
- 作为 `Assets/ThirdParty` 中的源码子集接入 GameplayAbilities 的 Core、Runtime、Editor，以及 GameplayTags、DeterministicMath、Hash、Logging 的必要程序集。排除上游可选的 AssetManagement、DataTable、网络和示例集成，避免强制使用第二套资源系统。
- UniTask 固定 2.5.0，保留 MIT 许可证及 `.meta`，只纳入需要的程序集；GameplayTags 编辑器所需 Newtonsoft JSON 使用 Unity Package Manager。
- 资源表现通过实现 GameplayAbilities 的 `IResourceLocator` 接入 GF。GF 不能取消已提交的加载；取消后收到的资源必须释放。正常完成时资源句柄只卸载一次。

## 生命周期与权威

同一个对局中需要相互作用的技能实体共享一个 `GASRuntimeContext`；调用方先释放 ASC，再释放 Context。GF 资源服务必须在未完成的加载和句柄全部释放前保持可用。对象和资源操作均由 Unity 主线程持有。

RTS Engine 可用后，由适配层把其单位和伤害接口映射到技能系统。生命值、死亡和普通攻击由 RTS Engine 作为权威来源；不得把两个系统的独立血量都作为权威。移动速度等 Buff 对普通单位的具体集成方式须在取得 RTS Engine 后依据真实 API 设计和测试。

## 验证与限制

在本机忽略目录运行行为测试，覆盖技能激活、资源消耗、冷却与效果过期；覆盖 GF 资源加载成功、失败、取消后迟到释放和重复释放。运行项目 Unity 回归并检查编译日志。Windows 开发版构建与标签读取已验证；其他平台、多人同步、运行性能、RTS 单位桥接和线上质量仍需单独验证。
