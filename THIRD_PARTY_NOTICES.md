# 第三方来源与修改说明

## Game Framework

- 上游：https://github.com/EllanJiang/GameFramework
- 固定配套版本：`v2021.05.31`
- 提交：`b2c3ba9a45fe4ea6a38165c0af807bc148bb8b32`
- 本工程目录：`Assets/Framework/GameFramework`
- 许可证：MIT，完整文本保留在该目录的 `LICENSE.md`。
- 调整：以源码程序集 GameFramework.asmdef 集成；保留核心源码与版权声明。

## Unity Game Framework

- 上游：https://github.com/EllanJiang/UnityGameFramework
- 固定配套版本：`v2021.05.31`
- 提交：`2f08fc081ea29c6578a815693b969b061be020e3`
- 本工程目录：`Assets/Framework/UnityGameFramework`
- 许可证：MIT，完整文本保留在该目录的 `LICENSE.md`。

对该上游的必要修改：

1. Runtime / Editor asmdef 显式引用源码 GameFramework 程序集，替代原先自动引用的 GameFramework.dll。
2. `DefaultCompressionHelper.cs` 使用 .NET GZipStream，移除古老 SharpZipLib DLL 依赖；保留调用者流的所有权，验证字节范围。标准 GZip 往返已经回归验证，不承诺兼容原游戏自定义加密或压缩格式。
3. `ProcedureComponent.cs` 将启动前的 WaitForEndOfFrame 改为 yield return null。它仍延后至其他组件完成 Start，且可以在无图形的批处理测试里运行。
4. 不携带编译版 GameFramework.dll 和压缩 DLL；保留 GF prefab 与 `.meta`，以源码构建。

这些变更以本工程行为和验证为准，不代表上游维护者对本底座提供支持。以后升级请保留许可证并核对差异。

## Unity Packages

`Packages/manifest.json` 声明 Unity UGUI、Unity Test Framework 和内置引擎模块。Unity Package Manager 下载的包遵循各自的许可证；测试包仅用于开发验证。包缓存不作为本底座源码打包分发。

## Starter 示例

`Assets/Starter`、`Assets/Game`、`docs` 与 `tools` 是为本次独立底座新增的项目代码、示例资产和说明。可作为你的新项目起点修改；对外分发时仍须保留 GF 的 MIT 许可证和上游声明。

## Spine Runtimes

- 本地来源：原项目 `D:\MergeCookAndroid\Assets\Spine`，本轮未升级或从其他版本混入 Runtime。
- 本工程目录：`Assets/ThirdParty/Spine`。
- 包内版本：spine-unity `4.1.43`、spine-csharp `4.1.2`，配套 Spine 4.1 素材。
- 上游：[Esoteric Software / spine-runtimes](https://github.com/EsotericSoftware/spine-runtimes)。
- 许可证：Spine Runtimes License Agreement，原版权头保留，另在 `Assets/ThirdParty/Spine/LICENSE.txt` 汇总原文。
- 本轮调整：Runtime 和 Editor asmdef 显式增加 Unity.ugui / Unity.ugui.Editor 引用。其他运行时、编辑器与 Shader 保持本地包来源。

Spine 的许可证独立于 GF 的 MIT；商业开发和再分发按 Spine 许可条款处理。示例使用程序生成的白色矩形骨架、atlas 和贴图，没有携带原游戏角色或 Spine Editor 安装文件。

## 原游戏通用脚本

`Assets/Extensions/UI` 的一部分控件源自原项目 UGUIExtend、UnityExtend 和 Utils；保留了原源码作者标注。引导、Spine 播放适配、粒子调度及若干交互组件经过通用化重构；对应关系见 `docs/migration-map.md`。这些源码未发现独立的开源许可声明，不能因为与 GF 放在同一工程就将其宣称为 MIT。作为原项目所有者的新项目内部复用起点使用，对外分发应按原项目权属处理。
