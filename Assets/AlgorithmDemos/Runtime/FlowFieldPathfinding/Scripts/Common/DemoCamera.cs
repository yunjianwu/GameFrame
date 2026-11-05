using UnityEngine;

namespace Demos.FlowFieldPathfinding
{
    public static class DemoCamera
    {
        public static Camera Setup(GameObject owner, Color background)
        {
            var cameraObject = new GameObject("Algorithm Camera");
            cameraObject.transform.SetParent(owner.transform, false);
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.depth = 20;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            Fit(cam);
            return cam;
        }

        public static void Fit(Camera cam) { cam.orthographicSize = Screen.height * 0.5f; }

        public static Vector2 ScreenToWorld(Vector2 screen)
        {
            return new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
        }
    }
}