using GameFramework;
using GameFramework.Event;
using ResourceMode = GameFramework.Resource.ResourceMode;
using Starter.Core;
using Starter.Runtime;
using Starter.Runtime.Startup;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureUpdateResource : ProcedureBase
    {
        private ResourceUpdateCoordinator _update;
        private EventScope _events;
        private bool _offline;
        private bool _confirmationShown;
        private float _idleElapsed;
        private long _lastBytes;
        private ResourceUpdateState _lastState;

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            _offline = GameServices.Base.EditorResourceMode || GameServices.Resource.ResourceMode == ResourceMode.Package;
            _confirmationShown = false;
            _idleElapsed = 0;
            _lastBytes = -1;
            _lastState = ResourceUpdateState.Idle;
            GameServices.Host.SetStatus("Checking local resources...");
            GameServices.Host.SetDownloadProgress(0, 0);
            if (_offline) return;
            _events = new EventScope(GameFrameworkEntry.GetModule<IEventManager>());
            _events.Subscribe(ResourceUpdateStartEventArgs.EventId, OnDownloadStart);
            _events.Subscribe(ResourceUpdateChangedEventArgs.EventId, OnDownloadProgress);
            _events.Subscribe(ResourceUpdateSuccessEventArgs.EventId, OnDownloadSuccess);
            _events.Subscribe(ResourceUpdateFailureEventArgs.EventId, OnDownloadFailure);
            var settings = GameServices.Host.StartupSettings;
            _update = new ResourceUpdateCoordinator(new GfResourceUpdateClient(GameServices.Resource),
                settings.RequiredResourceGroups, settings.RequireDownloadConfirmation);
            _update.Begin();
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            if (_offline) { ChangeState<ProcedurePreload>(owner); return; }
            _update.Tick();
            GameServices.Host.SetDownloadProgress(_update.DownloadedBytes, _update.TotalDownloadBytes);
            if (_update.State == ResourceUpdateState.Completed) { ChangeState<ProcedurePreload>(owner); return; }
            if (_update.State == ResourceUpdateState.Failed)
            {
                string error = _update.Error;
                Release();
                GameServices.Host.ReportFailure("Resource update failed: " + error);
                return;
            }
            if (_update.State == ResourceUpdateState.AwaitingConfirmation)
            {
                _idleElapsed = 0;
                if (!_confirmationShown)
                {
                    _confirmationShown = true;
                    GameServices.Host.SetStatus("Resources need an update. Confirm the download to continue.");
                    GameServices.Host.SetDownloadConfirmation(_update.Confirm);
                }
                return;
            }
            if (_lastState != _update.State || _lastBytes != _update.DownloadedBytes) _idleElapsed = 0;
            else _idleElapsed += realElapsed;
            _lastState = _update.State;
            _lastBytes = _update.DownloadedBytes;
            if (_update.State == ResourceUpdateState.Updating)
                GameServices.Host.SetStatus("Updating resource group: " + (string.IsNullOrEmpty(_update.CurrentGroup) ? "all" : _update.CurrentGroup));
            if (_idleElapsed > GameServices.Host.StartupSettings.OperationTimeoutSeconds)
                _update.Abort("No resource check/download progress before the startup timeout.");
        }

        private void OnDownloadStart(object sender, GameEventArgs args)
        {
            if (!IsCurrent(Generation) || _update == null) return;
            var data = (ResourceUpdateStartEventArgs)args;
            _update.ReportPartial(data.Name, data.CurrentLength);
            _idleElapsed = 0;
        }

        private void OnDownloadProgress(object sender, GameEventArgs args)
        {
            if (!IsCurrent(Generation) || _update == null) return;
            var data = (ResourceUpdateChangedEventArgs)args;
            _update.ReportPartial(data.Name, data.CurrentLength);
        }

        private void OnDownloadSuccess(object sender, GameEventArgs args)
        {
            if (!IsCurrent(Generation) || _update == null) return;
            _update.ForgetPartial(((ResourceUpdateSuccessEventArgs)args).Name);
            _idleElapsed = 0;
        }

        private void OnDownloadFailure(object sender, GameEventArgs args)
        {
            if (!IsCurrent(Generation) || _update == null) return;
            var data = (ResourceUpdateFailureEventArgs)args;
            _update.ReportFailure(data.Name, data.RetryCount, data.TotalRetryCount, data.ErrorMessage);
            _idleElapsed = 0;
            Log.Warning("Resource '{0}' attempt {1}/{2} failed: {3}", data.Name, data.RetryCount + 1, data.TotalRetryCount + 1, data.ErrorMessage);
            // GF v2021.05.31 can omit group completion after its last failure. Tick handles the terminal event.
        }

        private void Release()
        {
            GameServices.Host?.SetDownloadConfirmation(null);
            _update?.Cancel();
            _update = null;
            _events?.Dispose();
            _events = null;
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            base.OnLeave(owner, isShutdown);
            Release();
        }
    }
}
