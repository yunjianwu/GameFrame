using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedurePlay : UiProcedure
    {
        protected override string UiAsset => AssetNames.Play;

        protected override void OnEnter(ProcedureOwner owner)
        {
            GameServices.Host.SetStatus("Playing");
            base.OnEnter(owner);
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (NavigateRequested && IsCurrent(Generation))
            {
                ProcedureChangeScene.Request(owner, AssetNames.MenuScene, SceneDestination.Menu);
                ChangeState<ProcedureChangeScene>(owner);
            }
        }
    }
}
