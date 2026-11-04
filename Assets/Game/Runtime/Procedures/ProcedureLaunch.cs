using System;
using GameFramework.Resource;
using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    /// <summary>Initialize the resource mode once. Place platform/service initialization here.</summary>
    public sealed class ProcedureLaunch : ProcedureBase
    {
        private bool _ready;
        private float _elapsed;

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            _ready = false;
            _elapsed = 0;
            int generation = Generation;
            GameServices.Host.SetStatus("Initializing resource mode...");
            try
            {
                if (GameServices.Host.StartupSettings == null) throw new InvalidOperationException("StartupSettings is missing from GameBootstrap. Run Starter/7. Configure Procedure Flow.");
                bool editor = GameServices.Base.EditorResourceMode;
                ResourceMode mode = GameServices.Resource.ResourceMode;
                bool remote = !editor && (mode == ResourceMode.Updatable || mode == ResourceMode.UpdatableWhilePlaying);
                GameServices.Host.StartupSettings.Validate(remote);
                if (editor || remote) _ready = true;
                else if (mode == ResourceMode.Package)
                    GameServices.Resource.InitResources(() => { if (IsCurrent(generation)) _ready = true; });
                else throw new InvalidOperationException("Select Package, Updatable or UpdatableWhilePlaying resource mode.");
            }
            catch (Exception exception) { GameServices.Host.ReportFailure("Resource startup: " + exception.Message); }
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            _elapsed += realElapsed;
            if (_ready) ChangeState<ProcedureCheckVersion>(owner);
            else if (_elapsed > GameServices.Host.StartupSettings.OperationTimeoutSeconds)
                GameServices.Host.ReportFailure("Package initialization timed out. Build and include matching GF Package resources.");
        }
    }
}
