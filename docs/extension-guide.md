# 扩展接入示例

## 增加一个界面

1. 在 `Assets/Game/Runtime/UI` 新建继承 `ScopedUIForm` 的脚本。
2. 新建带 RectTransform、Canvas、GraphicRaycaster 的 UGUI 预设，挂脚本，通过 SerializeField 引用控件。
3. 在 OnInit 绑定生命周期内固定的按钮监听；在 OnOpen 读取本次参数和订阅事件；在 OnClose 释放本次业务状态，再调用基类。
4. 在 AssetNames 加资源路径，并通过 GF Resource Editor 将预设加入资源包。
5. 用 UIComponent.OpenUIForm 打开，保存返回的 serialId，在拥有它的流程离开时关闭或取消加载。

参考 `MenuForm.cs` 和 `PlayForm.cs`。当前 UI 分组为 Default、Popup、Overlay，深度分别为 0、1000、2000。ScopedUIForm 根据 GF 组深度和组内排序更新 Canvas，项目如果有复杂弹窗栈、遮罩或转场，可在这个边界扩展。

```csharp
protected override void OnOpen(object userData)
{
    base.OnOpen(userData);
    Events.Subscribe(MyEventArgs.EventId, OnChanged);
    RefreshFromModel();
}

protected override void OnClose(bool isShutdown, object userData)
{
    // Release this opening's request handles and references here.
    base.OnClose(isShutdown, userData);
}
```

不要在每次 OnOpen 无条件 AddListener。GF 复用预设实例时会累积按钮回调。不要只在 OnDestroy 解绑全局事件，关闭的 UI 可能一直留在池中。

## 增加一个业务事件

参考 `CoinsChangedEventArgs.cs`：继承 GameEventArgs，分配稳定且唯一的 ID，通过 ReferencePool.Acquire 创建，在 Clear 中清理所有字段。业务服务完成模型修改后派发通知，界面用 EventScope 持有订阅。

本示例金币事件 ID 为 10001。团队应约定 ID 分段或统一生成，避免多个模块手写相同数字。不要依赖 string.GetHashCode 作为跨进程或持久化协议 ID。

EventScope 只管理通过自身创建的订阅，不会解除其他模块的监听。Subscribe 和 Dispose 要在创建作用域的线程调用。事件回调里只读取/复制值；参数对象会回池。

## 增加一个流程

继承项目的 `Starter.Game.Procedures.ProcedureBase`，实现 OnEnter / OnUpdate / OnLeave，使用 ChangeState 切换。基类提供每次进入的代号与活动检查。切换依据放进字段，在 OnUpdate 决策；不要从不确定线程的网络回调直接切换流程。完整主链与扩展位置见 `procedures.md`。

在 Boot 场景的 ProcedureComponent 中，把流程完整类型名加入 Available Procedures，并保持 Entrance 为 ProcedureBoot，或显式替换你的入口。只新增 .cs 不会自动进入流程列表。Starter 生成器在场景已存在时只验证；`Starter/7. Configure Procedure Flow` 会追加模板缺少的主链类型，不会替你注册任意新增业务流程。

反射创建的流程、辅助器在 IL2CPP 下需要保留。当前 link.xml 为起步保留整个运行时程序集，正式发布再收紧。

## 增加配置与存档

小配置可以复用 Preload 的 TextAsset + JsonUtility 方式，并做字段校验。JsonUtility 不支持所有 .NET 类型；字典、复杂多态等需求应选择合适的序列化方案。

大规模策划表建议引入生成工具和 DataTable，建立源表、生成代码、配置版本的一致性校验。配置是只读输入；玩家存档是有版本的运行状态，不要混用一个 JSON。

Setting 当前用于示例金币和小设置。正式存档应加入版本迁移、原子替换、损坏恢复与业务校验。涉及安全和付费的状态，应由服务端或对应可信业务协议确认。

## 接入 SDK / Lua / 网络

建议新增 `Assets/Platform/Runtime` 程序集，为登录、广告、支付分别定义小接口，Android、WebGL、微信和抖音各自实现。SDK 回调不要直接修改界面；先通过 MainThreadQueue 返回主线程，再交给业务服务。

Lua 建议放在独立程序集，通过项目层适配接口接 GF 资源与 UI，不修改 GF 核心。Lua 虚拟机的线程约束必须由对应桥接实现确认，本底座没有带入 ToLua 或原游戏的桥接代码。

TCP 接入要实现 INetworkChannelHelper、包头、编解码和 Handler。异步 Socket 的 DeserializePacketHeader / DeserializePacket 可能在后台执行，Handler 则通过 GF 收包队列在主循环执行。解析层只处理数据；自定义连接事件直接订阅和 Unity 包装事件的派发线程也应分别确认。

小游戏通常需要 WebSocket 与平台 SDK，不要直接将原生 Socket 代码当作 WebGL 通用方案。

## 接入发布热更新

已经带入 ProcedureCheckVersion / ProcedureUpdateResource：版本 JSON、GF 清单更新、资源检查、下载确认、按组下载、进度、有限重试和失败阻断均已接线，完成后才进入 Preload。默认 Editor / Package 跳过远端分支。

在 `Assets/Game/Settings/StartupSettings.asset` 配置端点和启动资源组，按 `procedures.md` 构建/部署 GF Full 资源，再将 Boot ResourceComponent 切到可更新模式。正式发布还要完善空间检查、离线策略、回滚、整包升级及签名验真，并在真实服务和设备上验证断网、部分下载和损坏资源。GF Resource 不会自动热更新 C#。
