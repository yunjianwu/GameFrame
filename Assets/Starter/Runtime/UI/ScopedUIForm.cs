using GameFramework;
using GameFramework.Event;
using Starter.Core;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Starter.Runtime.UI
{
    /// <summary>GF 界面会被复用。每次 OnOpen 订阅、OnClose 解绑，不能只在 OnDestroy 解绑。</summary>
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    public abstract class ScopedUIForm : UIFormLogic
    {
        protected EventScope Events { get; private set; }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            Events?.Dispose();
            Events = new EventScope(GameFrameworkEntry.GetModule<IEventManager>());
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            Events?.Dispose();
            Events = null;
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            base.OnDepthChanged(uiGroupDepth, depthInUIGroup);
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = uiGroupDepth + depthInUIGroup;
        }

        protected virtual void OnDestroy() { Events?.Dispose(); }
    }
}
