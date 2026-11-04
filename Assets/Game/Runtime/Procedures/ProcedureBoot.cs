using Starter.Runtime;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureBoot : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            GameServices.Bind();
            GameServices.Host.SetStatus("Framework ready");
            Log.Info("GF Starter booted. Business and Unity APIs run on the main thread.");
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            ChangeState<ProcedureLaunch>(owner);
        }
    }
}
