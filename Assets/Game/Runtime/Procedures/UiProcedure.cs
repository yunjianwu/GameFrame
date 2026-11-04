using GameFramework;
using GameFramework.Event;
using Starter.Core;
using Starter.Runtime;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public abstract class UiProcedure : ProcedureBase
    {
        public override bool UseNativeDialog => false;
        private int _serialId;
        private EventScope _events;
        protected bool NavigateRequested;
        protected abstract string UiAsset { get; }

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            NavigateRequested = false;
            _events = new EventScope(GameFrameworkEntry.GetModule<IEventManager>());
            _events.Subscribe(OpenUIFormFailureEventArgs.EventId, OnOpenFailed);
            _serialId = GameServices.UI.OpenUIForm(UiAsset, AssetNames.DefaultUIGroup, (object)new System.Action(RequestNavigation));
        }

        private void RequestNavigation() { NavigateRequested = true; }

        private void OnOpenFailed(object sender, GameEventArgs args)
        {
            var failure = (OpenUIFormFailureEventArgs)args;
            if (failure.SerialId == _serialId)
                GameServices.Host.ReportFailure("Cannot open UI: " + failure.ErrorMessage);
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            // 关闭尚在加载中的窗口会告诉 GF：加载完成后直接回收，避免切换后旧界面又弹出来。
            if (!isShutdown && (GameServices.UI.HasUIForm(_serialId) || GameServices.UI.IsLoadingUIForm(_serialId)))
                GameServices.UI.CloseUIForm(_serialId);
            _events?.Dispose();
            _events = null;
            base.OnLeave(owner, isShutdown);
        }
    }
}
