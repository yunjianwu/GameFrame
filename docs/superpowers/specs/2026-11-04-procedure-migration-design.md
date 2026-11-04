# Procedure 迁移设计

用户已要求把原工程 Procedure 一并迁入独立底座。本次在现有启动链上补齐资源启动与场景切换职责，不带原游戏的 Lua、SDK、服务器地址及业务资源。

## 最终链路

`Boot -> Launch -> CheckVersion -> UpdateResource -> Preload -> EnterGame -> ChangeScene -> Menu / Play`

Menu 与 Play 都通过 ChangeScene 切换。Boot 保持常驻；两个示例业务场景由 GF Scene 加载和卸载，界面仍由各自业务流程拥有。

## 启动职责

- Boot 绑定 GF 组件，Launch 校验启动设置并初始化 Package 资源。
- Editor Resource Mode 和 Package 跳过远端版本下载；Updatable / UpdatableWhilePlaying 请求显式配置的 HTTP(S) JSON，校验资源版本与清单长度，调用官方 CheckVersionList / UpdateVersionList。
- UpdateResource 调用官方 CheckResources，按设置中的资源组顺序更新；空组列表更新全部资源。使用 GF 资源组集合统计去重后的大小。支持下载确认、下载进度和 GF 的每资源有限重试，失败不会进入业务。
- Preload 只加载业务配置，EnterGame 是项目初始化扩展位置。
- 原 ProcedureBase 的停止声音、隐藏实体职责迁入新基类，场景异步卸载等待放在 ChangeScene。UseNativeDialog 有实际默认值，不保留原来的异常实现。
- 原 LoadLuaScripts / ExecLuaScripts 是空壳，改为文档中的可选 Lua 接入位置。

## 生命周期与失败

流程切换只发生在 OnUpdate。异步回调检查当次进入标识，离开解除事件和确认回调，释放请求。资源失败显示不依赖资源的宿主面板，允许重启应用后利用 GF 已完成缓存继续检查；不在同一次失败中重复启动 GF 检查器。版本 HTTP 请求有限重试；清单失败明确结束。场景事件通过每次请求独立 token 归属。

## 工程边界

- 原项目 `D:\MergeCookAndroid` 只读；目标 `D:\workspace\UnityGFStarter` 无 Git，不初始化或提交。
- 不删除文件，不修改 AGENTS、凭据或环境配置，不安装依赖。
- GF 固定 `v2021.05.31`，Unity 保持 `2022.3.62f2c1`。
- 产物和日志只写 `E:\AI\_DownLoads\unity-gf-starter`。
- 保留现有 UI、引导、Spine、Effects 和扩展示例资产。

## 验证

先增加当前工程会失败的启动接线断言。更新协调器用可控资源后端验证确认、顺序、失败和迟到回调；真实 Boot 测试验证业务场景卸载/加载和 UI 生命周期。执行全部现有 EditMode / PlayMode，重新构建 Windows Package 并运行开发包 smoke。真实 CDN、断网恢复策略、回滚和 Android/WebGL 设备验证不由离线测试代替。
