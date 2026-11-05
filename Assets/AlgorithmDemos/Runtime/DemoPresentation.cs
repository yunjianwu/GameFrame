using UnityEngine;

namespace AlgorithmDemos
{
    public static class DemoPresentation
    {
        private static Font _font;
        private static bool _fontResolved;

        public static void BeginGui()
        {
            if (!_fontResolved)
            {
                _fontResolved = true;
                // Use the installed desktop font without redistributing an OS font file.
                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "Noto Sans CJK SC", "PingFang SC", "Arial" }, 14);
            }
            if (_font != null) GUI.skin.font = _font;
        }
    }
}
