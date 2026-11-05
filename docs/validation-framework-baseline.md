# 底座历史验证记录（2026-11-04）

以下保留 UnityGFStarter 导出时的验证记录。当前整合工程的验收以 validation.md 为准。

# 验证记录

验证日期：2026-11-04。以下是已经执行的检查，不把源码存在或平台映射当作平台验证通过。

## 环境

- Windows 11 x64。
- Unity 安装目录：`C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe`。
- Unity 运行时报告：`2022.3.62f2c1 (92e6e6be66dc)`。
- .NET SDK：10.0.201，仅用于独立核心回归工具；Unity 自己使用编辑器配套的 C# 工具链。
- 渲染管线：Built-in。独立播放器验收使用 Windows64、Mono、Development Build。

## 已通过

| 检查 | 实际结果 | 覆盖内容 |
|---|---|---|
| 独立纯 C# 回归 | 30 passed / 0 failed；0 编译警告、0 编译错误 | 原 16 项核心契约，以及版本参数、资源确认、按组推进、进度、失败、取消和迟到回调 14 项 |
| Unity 导入与资产生成 | 成功 | 官方源码程序集、Boot、五页 ExtensionGallery、UI 预设、引导配置、生成的 Spine 4.1 骨架与资源集合 |
| Unity 资产验证 | 成功 | 基础与扩展示例；预设没有 Missing Script，Graphic 的 CanvasRenderer 和 Spine 骨架引用有效 |
| Unity EditMode | 35 passed / 0 failed / 0 skipped | 30 项共享核心/资源启动契约，以及 UI 网格、圆角、RadioButton、富文本图片解析等 5 项检查 |
| Unity PlayMode | 28 passed / 0 failed / 0 skipped | 完整 Boot 链、GF 业务场景加载/卸载、五页场景联动、UI 生命周期和回收、引导、Spine、粒子 |
| GF Windows64 Package 构建 | 成功 | 版本清单、配置包、UI 包、Menu/Play 场景包与资源依赖 |
| Windows64 独立播放器构建 | 成功 | Boot 与 ExtensionGallery 均入包；外部资源通过构建处理器加入 StreamingAssets |
| 独立播放器自检 | 退出码 0，成功标记已输出 | 完整 Procedure 启动链；从 Boot 实际加载 Package，切换 GF 业务场景并卸载旧场景，显示菜单/Play 实例，点击加金币，模型和 Text 值一致 |
| 上游差异和来源检查 | 符合修改清单 | 核心 321 个 C# 文件内容未改；Unity 侧仅 2 个 asmdef、压缩 Helper、流程启动等待发生修改 |
| 独立性检查 | 未发现原游戏运行时依赖 | 无 Lua/SDK 引用、原游戏包名、签名库或携带的 GF 编译 DLL |
| 原项目 UI 源文件保护 | 31 个文件哈希均未变化 | 对照迁移前记录的 SHA256，读取源项目而不写回 |
| 原项目 Procedure 保护 | 7 个文件哈希均未变化 | 原流程只读分析，在独立工程中适配 |

PlayMode 集成用例还断言：打开 Play 时金币事件订阅恰好 1 个，关闭后为 0，重开后仍为 1；重开后的点击只增加一次奖励。这验证了 GF 界面池复用下的事件和按钮生命周期。用例会恢复启动时的金币值，避免每次测试累加存档。

Procedure 迁移先在原底座运行新增的真实场景断言，观察到 Menu 业务场景未加载的预期失败（27 passed / 1 failed）。迁移后单独 Boot 回归及完整 PlayMode 均通过：进入 Menu/Play 时 GF 已加载对应场景，旧业务场景已卸载，Boot 仍保留。设置资产、9 个流程注册、两个业务场景及其资源分包也通过了实际 Unity 验证。

资源启动的共享回归使用可控外部资源客户端，实际运行协调器，覆盖确认前不下载、组间只在 Tick 推进、空列表全量更新、已就绪跳过、重复组去重、重试进度替换、失败不进入下一组、取消后迟到检查/下载回调无效。版本参数检查覆盖缺少/无效目录 URL、版本和长度，以及有符号 CRC；这些不等于已验证远端服务。

核心测试先运行缺失实现的版本观察到失败，再实现并跑通。共享测试套件同时由 Unity NUnit 和独立命令行工具调用；不是维护两份互不一致的测试。

扩展 PlayMode 覆盖 Stop 后的晚到目标、重新启用解绑、存档恢复、Signal 展示期间的单次事件、列表回收与重复初始化、分组数据增减、详情开合后的内容和点击索引、空/可变尺寸/横向列表吸附。Spine 覆盖完成与中断、旧句柄取消保护新轨道、队列取消、轨道与起播秒数分离、Slot 透明度、序列失败清理，以及 completed 参数和公开 Finished 事件中再次播放的两种重入。粒子用例覆盖托管注册与重新启用、颜色过渡终点及停用恢复。

最终复核的 8 个新增回归先在修复前观察到预期失败，再分别修复并通过。完整场景用例还走通点击 → 拖动 → 业务信号引导、四种列表、Spine 两次播放接循环，以及三类粒子注册。

## 报告位置

最新完整 Unity 验证：

```text
E:\AI\_DownLoads\unity-gf-starter\build\verification\20261104-184331-c777ff68\
  validate.log
  extensions.log
  EditMode.log
  EditMode.xml
  PlayMode.log
  PlayMode.xml
```

独立播放器与构建日志：

```text
E:\AI\_DownLoads\unity-gf-starter\build\player-validation\20261104-184601-f0202afe\windows-build.log
E:\AI\_DownLoads\unity-gf-starter\build\player-validation\20261104-184601-f0202afe\player-smoke.log
E:\AI\_DownLoads\unity-gf-starter\build\packages\20261104-184618-a8e6beb2\player\UnityGFStarter.exe
```

成功标记：`STARTER_PROCEDURES_CONFIGURED:`、`STARTER_VALIDATED:`、`STARTER_EXTENSIONS_VALIDATED:`、`STARTER_PLAYER_BUILT:`、`STARTER_PLAYER_SMOKE_PASSED:`。

迁移前的源文件哈希记录：`E:\AI\_DownLoads\unity-gf-starter\build\migration\source-ui-baseline.json`、`source-procedure-baseline.json`。

## 遇到并处理的问题

源码替代 DLL 后，上游 Unity Runtime / Editor 的 asmdef 需要显式引用 GameFramework；已经补齐。上游 Procedure 的 WaitForEndOfFrame 在无图形 PlayMode 下无法恢复，导致启动等待超时；已经改成等待下一帧，随后集成用例通过。

EditMode 曾在写出 Passed XML 后卡在退出阶段；最新完整验证也出现这项情况，脚本在结果完成后等待 20 秒，只回收自己创建的 Unity 进程并输出警告，然后独立校验 XML 中的用例数、结果和失败数：35 项全部通过。最新 validate、extensions、PlayMode 正常退出。缺失、空或失败的测试结果不会被当作通过。

扩展迁移时修复了 Graphic 缺少 CanvasRenderer、生成 SkeletonGraphic 引用保存不完整、Spine 4.1 测试骨架缺少 hash/atlas、列表回收后未重新绑定、刷新分组后滚动范围过期，以及 Spine 同步中断事件中的重入。修复均落在独立工程，没有修改原游戏。只读复核对最后的修正未再发现重要问题；运行结果仍以以上 XML 和构建日志为准。

本次 Procedure 只读复核发现固定 GF 版本在最后一个资源下载失败时可能不派发组完成回调。已在最终重试失败事件记录具体资源与错误，下一 Tick 停止更新并结束启动；新增“没有组完成回调”的回归先观察到失败，再修复通过。复核确认 Important 问题解决。批处理接线工具还处理了 Unity 启动时干净的 Untitled 场景不能直接创建附加场景的问题；实际 Configure 命令已成功执行。

`-nographics` 使用 Null 图形设备，日志含 Shader 不受该设备支持的提示；编辑器许可客户端启动也有握手和令牌更新提示，相关检查结果以上述 XML 和成功标记为准，EditMode 退出阶段的处理见前述记录。这项验收检查资源和交互逻辑，没有证明实际 GPU 渲染效果。Spine 骨架/Shader、遮罩、触摸与粒子视觉仍应在有图形的编辑器和目标设备验收。

## 尚未验证

- Android / iOS 真机、IL2CPP、微信/抖音小游戏、浏览器 WebGL 和设备性能。
- 不同屏幕比例、刘海/安全区、触摸交互、正式中文字体与视觉效果。
- Spine 4.1 商业素材兼容性、不同 Canvas 坐标/遮挡组合、复杂多行列表、真实 GPU 粒子模拟耗时与 Overdraw。
- CDN 热更新、断点下载、资源回滚、Lua、广告/登录/支付和线上服务。
- 原游戏自定义加密资源的兼容性。新底座采用官方资源链路，需重新构建资源。

## 复跑

先关闭本工程的编辑器，避免两个 Unity 进程争用项目锁。

```powershell
& .\tools\Verify-Core.ps1
& .\tools\Verify-Unity.ps1

# 单独检查其中一部分
& .\tools\Verify-Unity.ps1 -Mode Validate
& .\tools\Verify-Unity.ps1 -Mode EditMode
& .\tools\Verify-Unity.ps1 -Mode PlayMode
& .\tools\Verify-Unity.ps1 -Mode PlayMode -TestFilter Starter.Tests.BootFlowTests
```

更换编辑器可传 `-UnityPath`，更换报告目录可传 `-OutputRoot`。测试脚本每次使用独立报告目录，保留之前的诊断记录。
