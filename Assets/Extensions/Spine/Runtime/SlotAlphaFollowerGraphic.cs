using Spine;
using Spine.Unity;
using UnityEngine;

namespace Starter.Extensions.Spine
{
    [RequireComponent(typeof(CanvasGroup)), DisallowMultipleComponent]
    public sealed class SlotAlphaFollowerGraphic : MonoBehaviour
    {
        public SkeletonGraphic skeletonGraphic;
        public string slotName = "label";
        private CanvasGroup group;
        private Slot slot;
        private string resolvedName;

        private void Awake() { group = GetComponent<CanvasGroup>(); }
        private void OnEnable()
        {
            if (skeletonGraphic == null) skeletonGraphic = GetComponentInParent<SkeletonGraphic>();
            if (skeletonGraphic == null) return;
            skeletonGraphic.UpdateComplete += Updated;
            skeletonGraphic.OnRebuild += Rebuilt;
            Apply();
        }
        private void OnDisable()
        {
            if (skeletonGraphic != null)
            {
                skeletonGraphic.UpdateComplete -= Updated;
                skeletonGraphic.OnRebuild -= Rebuilt;
            }
            slot = null;
        }
        private void Updated(ISkeletonAnimation animation) { Apply(); }
        private void Rebuilt(SkeletonGraphic graphic) { slot = null; Apply(); }
        public void Apply()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (skeletonGraphic == null || skeletonGraphic.Skeleton == null) return;
            if (slot == null || resolvedName != slotName)
            {
                slot = skeletonGraphic.Skeleton.FindSlot(slotName);
                resolvedName = slotName;
            }
            if (slot != null) group.alpha = slot.A;
        }
    }
}
