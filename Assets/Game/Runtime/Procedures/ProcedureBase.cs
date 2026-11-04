using Starter.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Starter.Game.Procedures
{
    public abstract class ProcedureBase : GameFramework.Procedure.ProcedureBase
    {
        public virtual bool UseNativeDialog => true;
        protected int Generation { get; private set; }
        private bool _active;

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            Generation++;
            _active = true;
        }

        protected bool IsCurrent(int generation) => _active && Generation == generation &&
            GameServices.Host != null && string.IsNullOrEmpty(GameServices.Host.Failure);

        protected void ClearTransientObjects()
        {
            GameServices.Sound.StopAllLoadingSounds();
            GameServices.Sound.StopAllLoadedSounds();
            GameServices.Entity.HideAllLoadingEntities();
            GameServices.Entity.HideAllLoadedEntities();
        }

        protected override void OnLeave(ProcedureOwner owner, bool isShutdown)
        {
            _active = false;
            base.OnLeave(owner, isShutdown);
        }
    }
}
