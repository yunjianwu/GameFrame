using System.Diagnostics;
using UnityEngine;

namespace Demos.FlowFieldPathfinding
{
    /// <summary>
    /// 流场寻路五步演示。右侧面板切换步骤、画笔（墙 / 沼泽 / 擦除）；左键拖动涂格子，右键移动终点。
    /// 「A* 对照」按钮让当前所有怪（不足 5000 时取 5000 个随机起点）各跑一次 A*，与重建一次流场的耗时对比。
    /// </summary>
    public sealed class FlowFieldDemo : MonoBehaviour
    {
        private static readonly string[] StepNames =
        {
            "第一步：代价场",
            "第二步：积分场",
            "第三步：向量场",
            "第四步：查表狂奔",
            "第五步：尸潮推挤",
        };

        private static readonly string[] BrushNames = { "画墙", "沼泽", "擦除" };
        private static readonly byte[] BrushCosts = { FlowField.CostWall, FlowField.CostSwamp, FlowField.CostEmpty };

        private const float PanelWidth = 280f;
        private const float HudHeight = 30f;

        [SerializeField] private int step;
        [SerializeField] private int population = 2000;
        [SerializeField] private int seed = 12345;

        private FlowField ff;
        private Crowd crowd;
        private AStar astar;
        private Camera cam;
        private ShapeBatch batch;

        private float cell = 24f;
        private float originX;
        private float originY;
        private int brush;
        private bool painting;
        private float spawnTimer;
        private float rebuildMs;
        private float crowdMs;
        private float fps = 60f;
        private string astarReport = "";
        private readonly float[] pulseR = new float[6];
        private readonly float[] pulseLife = new float[6];
        private System.Random rng;

        private GUIStyle numberStyle;
        private Rect PanelRect => new Rect(Screen.width - PanelWidth - 10f, 10f, PanelWidth, 330f);

        public FlowField Field => ff;
        public Crowd Crowd => crowd;

        private void Start()
        {
            cam = DemoCamera.Setup(gameObject, new Color32(7, 9, 15, 255));
            batch = new ShapeBatch("FlowFieldBatch");
            rng = new System.Random(seed);
            ff = new FlowField(FlowMap.Cols, FlowMap.Rows);
            FlowMap.BuildDefault(ff);
            astar = new AStar(ff);
            Layout();
            crowd = new Crowd(ff, cell, seed);
            SetStep(step);
        }

        private void OnDestroy()
        {
            batch?.Destroy();
        }

        /// <summary>地图占据面板左侧区域，格子尽量大且保持正方形</summary>
        private void Layout()
        {
            float mapW = Screen.width - PanelWidth - 20f;
            float mapH = Screen.height - HudHeight;
            float newCell = Mathf.Max(8f, Mathf.Floor(Mathf.Min(mapW / FlowMap.Cols, mapH / FlowMap.Rows)));
            if (crowd != null && !Mathf.Approximately(newCell, cell)) crowd.Rescale(newCell);
            cell = newCell;
            originX = Mathf.Floor((mapW - FlowMap.Cols * cell) / 2f);
            originY = Mathf.Floor((mapH - FlowMap.Rows * cell) / 2f);
        }

        private void SetStep(int i)
        {
            step = i;
            if (i <= 3) crowd.Agents.Clear();
            if (i == 3) spawnTimer = 2f;
            crowd.Separation = i == 4;
        }

        // ---------------- 坐标换算 ----------------
        // 地图像素：左上角原点、Y 向下（与 IMGUI 一致）；世界坐标：屏幕中心原点、Y 向上

        private Vector2 MapToWorld(float px, float py)
        {
            return new Vector2(originX + px - Screen.width * 0.5f, Screen.height * 0.5f - (originY + py));
        }

        private void CellRect(int c, int r, Color32 color)
        {
            Vector2 bl = MapToWorld(c * cell, (r + 1) * cell);
            batch.Rect(bl.x, bl.y, cell, cell, color);
        }

        private bool MouseCell(out int c, out int r)
        {
            Vector2 g = DemoInput.GuiMousePosition;
            c = Mathf.FloorToInt((g.x - originX) / cell);
            r = Mathf.FloorToInt((g.y - originY) / cell);
            return ff.InBounds(c, r);
        }

        // ---------------- 主循环 ----------------
        private void Update()
        {
            DemoCamera.Fit(cam);
            Layout();
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

            HandleInput();
            Simulate(Mathf.Min(Time.deltaTime, 1f / 20f));
            Render();
        }

        private void HandleInput()
        {
            bool overPanel = PanelRect.Contains(DemoInput.GuiMousePosition);
            if (DemoInput.GetButtonUp(DemoInput.Left)) painting = false;
            if (overPanel) return;

            if (DemoInput.GetButtonDown(DemoInput.Right) && MouseCell(out int tc, out int tr) && ff.Cost[tc, tr] != FlowField.CostWall)
            {
                var sw = Stopwatch.StartNew();
                ff.SetTarget(tc, tr);
                rebuildMs = (float)sw.Elapsed.TotalMilliseconds;
            }

            if (DemoInput.GetButtonDown(DemoInput.Left)) painting = true;
            if (painting && DemoInput.GetButton(DemoInput.Left) && MouseCell(out int c, out int r))
            {
                byte cost = BrushCosts[brush];
                if (ff.Cost[c, r] != cost && !(c == ff.TargetX && r == ff.TargetY))
                {
                    ff.Cost[c, r] = cost;
                    var sw = Stopwatch.StartNew();
                    ff.Rebuild();
                    rebuildMs = (float)sw.Elapsed.TotalMilliseconds;
                }
            }
        }

        private void Simulate(float dt)
        {
            for (int i = 0; i < pulseR.Length; i++)
            {
                if (pulseLife[i] <= 0f) continue;
                pulseR[i] += dt * 120f;
                pulseLife[i] -= dt * 1.2f;
            }
            if (step < 3) return;

            var sw = Stopwatch.StartNew();
            if (step == 3)
            {
                spawnTimer += dt;
                if (spawnTimer >= 2f)
                {
                    spawnTimer = 0f;
                    crowd.SpawnRandom(1);
                }
            }
            else
            {
                int missing = population - crowd.Agents.Count;
                if (missing > 0) crowd.SpawnRandom(Mathf.Min(missing, 200), 0, 10);
            }

            int arrived = crowd.Step(dt);
            if (arrived > 0 && step == 4) EmitPulse();
            crowdMs = Mathf.Lerp(crowdMs, (float)sw.Elapsed.TotalMilliseconds, 0.1f);
        }

        private void EmitPulse()
        {
            for (int i = 0; i < pulseR.Length; i++)
            {
                if (pulseLife[i] > 0f) continue;
                pulseR[i] = cell * 0.5f;
                pulseLife[i] = 1f;
                return;
            }
        }

        // ---------------- 绘制 ----------------
        private float MaxIntegration()
        {
            float max = 1f;
            foreach (float v in ff.Integration)
                if (!float.IsInfinity(v) && v > max) max = v;
            return max;
        }

        private void Render()
        {
            batch.Clear();

            for (int x = 0; x < ff.Cols; x++)
            {
                for (int y = 0; y < ff.Rows; y++)
                {
                    byte c = ff.Cost[x, y];
                    CellRect(x, y, c == FlowField.CostWall ? new Color32(58, 63, 75, 255)
                                 : c == FlowField.CostSwamp ? new Color32(59, 42, 107, 255)
                                 : new Color32(17, 21, 31, 255));
                }
            }

            if (step >= 1)
            {
                float max = MaxIntegration();
                for (int x = 0; x < ff.Cols; x++)
                {
                    for (int y = 0; y < ff.Rows; y++)
                    {
                        float v = ff.Integration[x, y];
                        if (float.IsInfinity(v)) continue;
                        float t = 1f - v / max;
                        CellRect(x, y, new Color32(0, 229, 255, (byte)(255 * (0.05f + t * t * 0.55f))));
                    }
                }
            }

            // 网格线
            var gridColor = new Color32(255, 255, 255, 10);
            for (int x = 0; x <= ff.Cols; x++) batch.Line(MapToWorld(x * cell, 0), MapToWorld(x * cell, ff.Rows * cell), 1f, gridColor);
            for (int y = 0; y <= ff.Rows; y++) batch.Line(MapToWorld(0, y * cell), MapToWorld(ff.Cols * cell, y * cell), 1f, gridColor);

            if (step == 2) DrawVectors();

            Vector2 target = MapToWorld((ff.TargetX + 0.5f) * cell, (ff.TargetY + 0.5f) * cell);
            float glow = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            batch.Circle(target, cell * 0.45f, new Color32(40, 255, 140, (byte)(255 * glow)), 20);
            for (int i = 0; i < pulseR.Length; i++)
                if (pulseLife[i] > 0f) batch.Ring(target, pulseR[i], 2f, new Color32(40, 255, 140, (byte)(255 * pulseLife[i])));

            if (step == 3) DrawAgentsArrow();
            if (step == 4) DrawAgentsGlow();

            batch.Draw(cam);
        }

        private void DrawVectors()
        {
            var gold = new Color32(255, 201, 74, 255);
            float len = cell * 0.34f;
            for (int x = 0; x < ff.Cols; x++)
            {
                for (int y = 0; y < ff.Rows; y++)
                {
                    FlowVector v = ff.Vectors[x, y];
                    if (v.IsZero) continue;
                    // 地图 Y 向下，世界 Y 向上：方向的 Y 分量取反
                    Vector2 dir = new Vector2(v.X, -v.Y);
                    Vector2 c = MapToWorld((x + 0.5f) * cell, (y + 0.5f) * cell);
                    Vector2 tip = c + dir * len;
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    batch.Line(c - dir * len, tip, 1.2f, gold);
                    batch.Triangle(tip, tip - dir * 4f + side * 3f, tip - dir * 4f - side * 3f, gold);
                }
            }
        }

        private void DrawAgentsArrow()
        {
            var red = new Color32(255, 90, 79, 255);
            foreach (Agent a in crowd.Agents)
            {
                Vector2 dir = new Vector2(a.VX, -a.VY);
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 p = MapToWorld(a.X, a.Y);
                batch.Triangle(p + dir * 5f, p - dir * 3f + side * 3f, p - dir * 3f - side * 3f, red);
            }
        }

        private void DrawAgentsGlow()
        {
            var ember = new Color32(255, 120, 40, 170);
            var ice = new Color32(110, 170, 255, 190);
            foreach (Agent a in crowd.Agents)
                batch.Circle(MapToWorld(a.X, a.Y), 3f, a.InSwamp ? ice : ember, 6);
        }

        // ---------------- A* 对照 ----------------
        private void RunAStarComparison()
        {
            int n = Mathf.Max(crowd.Agents.Count, 5000);
            int[] sx = new int[n];
            int[] sy = new int[n];
            int filled = 0;
            foreach (Agent a in crowd.Agents)
            {
                sx[filled] = crowd.CellX(a.X);
                sy[filled] = crowd.CellY(a.Y);
                filled++;
            }
            while (filled < n)
            {
                int c = rng.Next(ff.Cols), r = rng.Next(ff.Rows);
                if (float.IsInfinity(ff.Integration[c, r])) continue;
                sx[filled] = c;
                sy[filled] = r;
                filled++;
            }

            var sw = Stopwatch.StartNew();
            long expanded = 0;
            for (int i = 0; i < n; i++) expanded += astar.Search(sx[i], sy[i], ff.TargetX, ff.TargetY).Expanded;
            double astarTotal = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            const int repeat = 20;
            for (int i = 0; i < repeat; i++) ff.Rebuild();
            double flow = sw.Elapsed.TotalMilliseconds / repeat;

            astarReport = $"A*：{n} 次搜索 {astarTotal:0.0} ms（平均展开 {expanded / n} 个节点）\n流场：重建一次 {flow:0.000} ms\n差 {astarTotal / flow:0} 倍";
        }

        // ---------------- UI ----------------
        private void OnGUI()
        {
            if (ff == null) return;
            AlgorithmDemos.DemoPresentation.BeginGui();
            if (step == 1 && cell >= 16f && Event.current.type == EventType.Repaint) DrawIntegrationNumbers();

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("流场寻路 Flow Field");
            int newStep = DemoGui.Choice(step, StepNames);
            if (newStep != step) SetStep(newStep);
            brush = DemoGui.Choice(brush, BrushNames, true);

            if (step == 3 && GUILayout.Button("一次放出 300 只（看共线问题）")) crowd.SpawnRandom(300);
            if (step == 4) population = DemoGui.IntSlider("同屏数量", population, 100, 10000, 100);
            if (step == 4 && crowd.Agents.Count > population) crowd.Agents.RemoveRange(population, crowd.Agents.Count - population);

            if (GUILayout.Button("A* 对照：给每只怪各跑一次 A*")) RunAStarComparison();
            if (astarReport.Length > 0) GUILayout.Label(astarReport);
            if (GUILayout.Button("重置地图"))
            {
                FlowMap.BuildDefault(ff);
                crowd.Agents.Clear();
            }
            GUILayout.Label("左键拖动：用当前画笔涂格子\n右键：把终点（绿色核心）移到该格");
            GUILayout.EndArea();

            DemoGui.HudLine($"{StepNames[step]} | 怪物 {crowd.Agents.Count} | FPS {fps:0} | 流场重建 {rebuildMs:0.00} ms | 怪物逻辑 {crowdMs:0.00} ms");
        }

        private void DrawIntegrationNumbers()
        {
            if (numberStyle == null)
            {
                numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
                numberStyle.normal.textColor = new Color(0.86f, 0.96f, 1f, 0.85f);
            }
            numberStyle.fontSize = Mathf.Max(7, Mathf.FloorToInt(cell * 0.36f));
            for (int x = 0; x < ff.Cols; x++)
            {
                for (int y = 0; y < ff.Rows; y++)
                {
                    float v = ff.Integration[x, y];
                    if (float.IsInfinity(v)) continue;
                    GUI.Label(new Rect(originX + x * cell, originY + y * cell, cell, cell), Mathf.RoundToInt(v).ToString(), numberStyle);
                }
            }
        }
    }
}
