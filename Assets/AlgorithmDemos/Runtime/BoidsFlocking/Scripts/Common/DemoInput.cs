using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Demos.BoidsFlocking
{
    /// <summary>
    /// 鼠标输入：Player Settings 的 Active Input Handling 为 Input System 或 Both 时走新输入系统，
    /// 为 Input Manager (Old) 时走旧 API。坐标统一为屏幕像素、左下角原点（与 Input.mousePosition 相同）。
    /// </summary>
    public static class DemoInput
    {
        public const int Left = 0;
        public const int Right = 1;

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool GetButton(int button)
        {
            if (AlgorithmDemos.DemoInputGate.IsBlocked(MousePosition)) return false;
#if ENABLE_INPUT_SYSTEM
            var b = GetControl(button);
            return b != null && b.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        public static bool GetButtonDown(int button)
        {
            if (AlgorithmDemos.DemoInputGate.IsBlocked(MousePosition)) return false;
#if ENABLE_INPUT_SYSTEM
            var b = GetControl(button);
            return b != null && b.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        public static bool GetButtonUp(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var b = GetControl(button);
            return b != null && b.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        /// <summary>IMGUI 坐标（左上角原点）下的鼠标位置，用来判断鼠标是否压在面板上</summary>
        public static Vector2 GuiMousePosition
        {
            get
            {
                var p = MousePosition;
                return new Vector2(p.x, Screen.height - p.y);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.ButtonControl GetControl(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;
            return button == Left ? mouse.leftButton : mouse.rightButton;
        }
#endif
    }
}
