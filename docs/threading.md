# 协程、多线程和回调边界

## 先看这张表

以下按本工程固定的 GF 源码实现区分。引擎内部是否使用工作线程，与业务回调在哪个线程执行是两个问题。

| 能力 | 本工程中的执行方式 | 业务接入规则 |
|---|---|---|
| Procedure 启动 | `ProcedureComponent.Start` 协程，等待下一帧后启动流程 | 流程回调在 GF 的主线程 Update 中执行 |
| 项目版本检查 | ProcedureCheckVersion 在宿主上运行 UnityWebRequest 等待协程和有限重试 | 协程在主线程恢复；回调只设就绪状态，OnUpdate 再切流程 |
| 项目资源更新/场景切换 | GF 异步请求 + 主循环事件与回调 | 不创建业务工作线程；退出后用进入代号/请求 token 忽略迟到结果 |
| Resource 读取字节、卸载场景 | DefaultResourceHelper 用协程等待引擎异步操作 | 可在主线程回调处理 Unity 对象；大解析不要误认为已经在后台 |
| AssetBundle 与资源加载 | 引擎异步请求 + GF 任务调度和辅助器 Update | 异步等待不等于业务代码多线程 |
| Sound 淡入淡出/延后停止暂停 | DefaultSoundAgentHelper 的协程 | AudioSource 仍由主线程操作 |
| WebRequest | UnityWebRequestAgentHelper 在 Update 检查请求完成 | 不需要为等待 HTTP 再套 Task.Run |
| Download | 下载辅助器跟踪 UnityWebRequest、处理进度和完成 | 引擎负责 IO；自定义 DownloadHandler 的回调上下文需要单独确认 |
| Network TCP 异步通道 | Socket BeginConnect / BeginSend / BeginReceive 的 APM 回调 | 网络收包、协议解析可能进入其他线程，不能直接操作 Unity 对象 |
| TCP 包的业务 Handler | 收包后进入 `m_ReceivePacketPool`，由 NetworkChannelBase.Update 派发 | 默认 GF Update 由主线程驱动；协议反序列化和最终 Handler 必须分开看 |
| Event.Fire | 入队，EventManager 下一次 Update 派发 | 默认在主线程消费；不会新建线程 |
| Event.FireNow | 在调用线程立即派发 | 调用者在后台，就会在后台执行监听者；本工程业务只在主线程用 |
| GF TaskPool / 对象池 / FSM | 普通 C# 管理器，由框架主循环驱动 | 不能因为名字有 Task 或 Pool，就认为线程安全 |
| Starter MainThreadQueue | TryPost 可跨线程，Update 的 Pump 在宿主主线程执行 | 后台只提交普通数据结果；队列满或销毁时 TryPost 返回 false |

源码定位：

- `Assets/Framework/UnityGameFramework/Scripts/Runtime/Procedure/ProcedureComponent.cs`
- `Assets/Framework/UnityGameFramework/Scripts/Runtime/Resource/DefaultResourceHelper.cs`
- `Assets/Framework/UnityGameFramework/Scripts/Runtime/Resource/DefaultLoadResourceAgentHelper.cs`
- `Assets/Framework/UnityGameFramework/Scripts/Runtime/Sound/DefaultSoundAgentHelper.cs`
- `Assets/Framework/UnityGameFramework/Scripts/Runtime/WebRequest/UnityWebRequestAgentHelper.cs`
- `Assets/Framework/GameFramework/Network/NetworkManager.TcpNetworkChannel.cs`
- `Assets/Framework/GameFramework/Network/NetworkManager.TcpWithSyncReceiveNetworkChannel.cs`
- `Assets/Framework/GameFramework/Network/NetworkManager.NetworkChannelBase.cs`
- `Assets/Framework/GameFramework/Base/EventPool/EventPool.cs`

同步收包的 TCP 通道与异步通道行为不同，接入时检查选用的 ServiceType 和 Helper。WebGL 不能直接照搬原生 Socket 通道，小游戏通常需要对应 SDK 或 WebSocket 适配。

## 协程适合做什么

协程在 `yield` 之间执行的代码仍占用主线程。适合等待资源、网络、动画、逐帧分批处理。

```csharp
private IEnumerator ProcessInBatches(int[] input, Action<int> apply)
{
    for (int i = 0; i < input.Length; i++)
    {
        apply(input[i]);
        if ((i + 1) % 100 == 0) yield return null;
    }
}
```

这是把工作摊到多帧，不会并行计算。每批的实际耗时仍要在目标设备上测量。超长单次 apply 仍会卡帧。

## 后台计算怎样安全回来

只有确认计算足够重，且平台支持线程时，才考虑 Task.Run。输入应是从 Unity 对象复制出来的普通数据快照；不要在后台读取 Transform、GameObject、资源管理器或 Lua 虚拟机。

下面是原生平台的接入示意，`Compute`、`Apply` 和 `ReportError` 由业务实现；未作为运行组件挂入示例：

```csharp
// Capture these on the main thread before starting work.
var host = GameServices.Host;
var queue = host.MainThread;
var token = host.Lifetime;
int[] snapshot = (int[])source.Clone();

Task.Run(() =>
{
    try
    {
        token.ThrowIfCancellationRequested();
        int result = Compute(snapshot, token);
        // TryPost can fail when the queue is full or the host has been destroyed.
        if (!queue.TryPost(() =>
        {
            if (!token.IsCancellationRequested) Apply(result);
        }))
        {
            // Drop a stale result, or record/retry it according to business needs.
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception error)
    {
        queue.TryPost(() =>
        {
            if (!token.IsCancellationRequested) ReportError(error);
        });
    }
}, token);
```

Compute 必须主动检查取消；传 token 不会强行停止已经开始的函数。退出、切换界面和切换业务会话可以有不同取消范围，宿主 Lifetime 只代表整个 GameRoot 的生命期。界面已经关闭时，不能仅凭宿主还活着就更新旧界面。

MainThreadQueue.Dispose 会清掉尚未执行的回调并拒绝后续提交；已开始运行的回调无法被强行撤回。每帧 64 个回调的数量预算不能阻止一个耗时很长的回调卡帧。

## WebGL 与小游戏

不把 Task.Run 作为通用后台方案。默认选择协程分帧和引擎异步 IO；只有平台、浏览器、编译设置和实际 SDK 均验证支持工作线程后才设计线程路径。Task / async / await 是任务和等待模型，本身不能证明执行线程发生了切换。

## 事件与池对象

示例 WalletService 在主线程调用 Fire，监听者在主线程 Update 更新 Text。GF 事件参数派发后会归还 ReferencePool。监听者只复制 Balance 等值，不能跨帧保留 CoinsChangedEventArgs，也不能交给另一个异步任务继续读。

Fire 的入队带锁，不代表 Subscribe / Unsubscribe、业务对象、UI 和所有 GF 模块都可以在任意线程调用。跨线程结果先回主线程，再操作事件和业务状态，更容易保持一致的生命周期与顺序。
