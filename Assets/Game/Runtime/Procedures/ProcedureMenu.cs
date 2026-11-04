using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureMenu : UiProcedure
    {
        protected override string UiAsset => AssetNames.Menu;

        protected override void OnEnter(ProcedureOwner owner)
        {
            GameServices.Host.SetStatus("Menu");
            GameServices.Host.CompleteStartup();
            base.OnEnter(owner);
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (NavigateRequested && IsCurrent(Generation))
            {
                ProcedureChangeScene.Request(owner, AssetNames.PlayScene, SceneDestination.Play);
                ChangeState<ProcedureChangeScene>(owner);
            }
        }
    }
}
