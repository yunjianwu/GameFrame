using UnityEngine;

namespace Demos.BoidsFlocking
{
    /// <summary>IMGUI 小工具：面板、带数值的滑条、单选按钮组。IMGUI 不依赖 Canvas / UGUI 包，两条渲染管线都能用。</summary>
    public static class DemoGui
    {
        private static GUIStyle selected;
        private static GUIStyle hud;

        public static GUIStyle SelectedButton
        {
            get
            {
                if (selected == null)
                {
                    selected = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                    selected.normal = selected.active;
                    selected.normal.textColor = new Color(0.55f, 0.8f, 1f);
                }
                return selected;
            }
        }

        public static GUIStyle Hud
        {
            get
            {
                if (hud == null)
                {
                    hud = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                    hud.normal.textColor = new Color(0.82f, 0.9f, 1f);
                }
                return hud;
            }
        }

        public static float Slider(string label, float value, float min, float max, string format = "0.0")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(84));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(value.ToString(format), GUILayout.Width(46));
            GUILayout.EndHorizontal();
            return value;
        }

        public static int IntSlider(string label, int value, int min, int max, int step = 1)
        {
            float v = Slider(label, value, min, max, "0");
            return Mathf.Clamp(Mathf.RoundToInt(v / step) * step, min, max);
        }

        public static bool Toggle(string label, bool value)
        {
            return GUILayout.Toggle(value, " " + label);
        }

        /// <summary>纵向按钮组，返回被选中的下标</summary>
        public static int Choice(int current, string[] names, bool horizontal = false)
        {
            if (horizontal) GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button(names[i], i == current ? SelectedButton : GUI.skin.button)) current = i;
            }
            if (horizontal) GUILayout.EndHorizontal();
            return current;
        }

        public static void HudLine(string text)
        {
            GUI.Label(new Rect(12, Screen.height - 28, Screen.width - 24, 24), text, Hud);
        }
    }
}
