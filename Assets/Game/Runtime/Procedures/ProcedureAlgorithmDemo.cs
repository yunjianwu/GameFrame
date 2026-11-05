using AlgorithmDemos;
using Starter.Game.UI;
using Starter.Runtime;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public sealed class ProcedureAlgorithmDemo : UiProcedure
    {
        protected override string UiAsset => AssetNames.DemoNavigation;
        private AlgorithmDemoScene _scene;
        private bool _restartRequested;

        protected override void OnEnter(ProcedureOwner owner)
        {
            _scene = AlgorithmDemoScene.Current;
            _restartRequested = false;
            base.OnEnter(owner);
            if (_scene == null || _scene.Simulation == null)
            {
                GameServices.Host.ReportFailure("The algorithm scene has no registered simulation.");
                return;
            }
            GameServices.Host.SetStatus("Demo: " + DemoCatalog.Get(_scene.Kind).Title);
        }

        protected override object CreateUserData()
        {
            int generation = Generation;
            return new DemoNavigation(
                _scene == null ? "ALGORITHM DEMO" : DemoCatalog.Get(_scene.Kind).Title,
                () => { if (IsCurrent(generation)) RequestNavigation(); },
                () => { if (IsCurrent(generation)) _restartRequested = true; });
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            if (!IsCurrent(Generation)) return;
            if (Input.GetKeyDown(KeyCode.Escape)) RequestNavigation();
            if (NavigateRequested)
                ProcedureChangeScene.Request(owner, AssetNames.MenuScene, SceneDestination.Menu);
            else if (_restartRequested)
                ProcedureChangeScene.Request(owner, DemoCatalog.Get(_scene.Kind).SceneAsset, SceneDestination.AlgorithmDemo, true);
            else return;
            ChangeState<ProcedureChangeScene>(owner);
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            base.OnLeave(owner, isShutdown);
            _scene = null;
        }
    }
}
