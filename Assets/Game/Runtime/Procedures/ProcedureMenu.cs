using AlgorithmDemos;
using Starter.Game.UI;
using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureMenu : UiProcedure
    {
        protected override string UiAsset => AssetNames.Menu;
        private DemoKind? _requestedDemo;

        protected override void OnEnter(ProcedureOwner owner)
        {
            _requestedDemo = null;
            GameServices.Host.SetStatus("Menu");
            GameServices.Host.CompleteStartup();
            base.OnEnter(owner);
        }

        protected override object CreateUserData()
        {
            int generation = Generation;
            return new MenuNavigation(
                () => { if (IsCurrent(generation)) RequestNavigation(); },
                kind => { if (IsCurrent(generation)) _requestedDemo = kind; });
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (_requestedDemo.HasValue && IsCurrent(Generation))
            {
                DemoDefinition demo = DemoCatalog.Get(_requestedDemo.Value);
                ProcedureChangeScene.Request(owner, demo.SceneAsset, SceneDestination.AlgorithmDemo);
                ChangeState<ProcedureChangeScene>(owner);
                return;
            }
            if (NavigateRequested && IsCurrent(Generation))
            {
                ProcedureChangeScene.Request(owner, AssetNames.PlayScene, SceneDestination.Play);
                ChangeState<ProcedureChangeScene>(owner);
            }
        }
    }
}
