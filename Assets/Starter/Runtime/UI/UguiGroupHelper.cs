using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Starter.Runtime.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class UguiGroupHelper : UIGroupHelperBase
    {
        private void Awake()
        {
            // GF skips SetDepth when a new group's depth is already zero.
            SetDepth(0);
        }

        public override void SetDepth(int depth)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = depth;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
