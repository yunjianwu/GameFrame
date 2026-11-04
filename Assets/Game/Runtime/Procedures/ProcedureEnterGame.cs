using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    /// <summary>Extend this boundary for save migration, login or optional Lua initialization after preload.</summary>
    public sealed class ProcedureEnterGame : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            GameServices.Host.SetStatus("Entering game...");
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            ProcedureChangeScene.Request(owner, AssetNames.MenuScene, SceneDestination.Menu);
            ChangeState<ProcedureChangeScene>(owner);
        }
    }
}
