using UnityEngine;

namespace AlgorithmDemos
{
    public static class DemoInputGate
    {
        public static RectTransform NavigationArea { get; set; }

        public static bool IsBlocked(Vector2 screenPosition)
        {
            return NavigationArea != null && NavigationArea.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(NavigationArea, screenPosition, null);
        }

        public static void Release(RectTransform area)
        {
            if (NavigationArea == area) NavigationArea = null;
        }
    }
}
