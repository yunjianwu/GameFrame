using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Demos.QuadTreeBoids
{
    /// <summary>
    /// 两个页签：
    /// 「四叉树实验室」左键点空白加点、按住已有点拖拽，橙框为鼠标处的范围查询；
    /// 「万鸟压测」最多 1 万只鸟，可一键切换暴力遍历 / 四叉树对比逻辑耗时，左键吸引、右键驱散。
    /// </summary>
    public sealed class QuadTreeDemo : MonoBehaviour
    {
        private enum Mode { Lab, Flock }

        private static readonly string[] ModeNames = { "四叉树实验室", "万鸟压测" };

        [Header("实验室")]
        [SerializeField] private int labCapacity = 4;
        [SerializeField] private float labQueryHalf = 80f;

        [Header("压测")]
        [SerializeField] private int boidCount = 2500;
        [SerializeField] private int capacity = 4;
        [SerializeField] private float perception = 50f;
        [SerializeField] private bool useQuadTree = true;
        [SerializeField] private bool showTree;
        [SerializeField] private int seed = 12345;

        private Mode mode = Mode.Lab;
        private Camera cam;
        private ShapeBatch batch;
        private Rect panelRect = new Rect(12, 12, 320, 250);

        private readonly List<Vector2> labPoints = new List<Vector2>();
        private readonly List<int> labHits = new List<int>();
        private QuadTree<int> labTree;
        private int hovered = -1;
        private int dragged = -1;
        private System.Random labRng;

        private FlockSimulation sim;
        private bool mouseActive;
        private bool mouseAttract;
        private Vector2 mouseWorld;
        private float fps = 60f;
        private float logicMs;

        private static readonly Color32 GridColor = new Color32(88, 166, 255, 90);
        private static readonly Color32 QueryColor = new Color32(255, 160, 60, 230);

        private void Start()
        {
            cam = DemoCamera.Setup(gameObject, new Color32(13, 17, 23, 255));
            batch = new ShapeBatch("QuadTreeBatch");
            labRng = new System.Random(seed);
            labTree = new QuadTree<int>(new Rectangle(0, 0, 1, 1), labCapacity);
            ResetLab();
            sim = new FlockSimulation(Screen.width, Screen.height, seed);
        }

        private void OnDestroy()
        {
            batch?.Destroy();
        }

        private void ResetLab()
        {
            labPoints.Clear();
            for (int i = 0; i < 10; i++)
                labPoints.Add(new Vector2((float)(labRng.NextDouble() - 0.5) * 800f, (float)(labRng.NextDouble() - 0.5) * 500f));
        }

        private void Update()
        {
            DemoCamera.Fit(cam);
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            mouseWorld = DemoCamera.ScreenToWorld(DemoInput.MousePosition);
            bool overPanel = panelRect.Contains(DemoInput.GuiMousePosition);

            batch.Clear();
            if (mode == Mode.Lab) UpdateLab(overPanel);
            else UpdateFlock(overPanel);
            batch.Draw(cam);
        }

        // ---------------- 实验室（原文阶段二 QuadTreeLab） ----------------
        private void UpdateLab(bool overPanel)
        {
            if (!overPanel && DemoInput.GetButtonDown(DemoInput.Left))
            {
                dragged = FindPoint(mouseWorld);
                if (dragged < 0) labPoints.Add(mouseWorld);
            }
            if (DemoInput.GetButtonUp(DemoInput.Left)) dragged = -1;
            if (dragged >= 0) labPoints[dragged] = mouseWorld;
            hovered = FindPoint(mouseWorld);

            // 点在拖动、结构随时会变，每帧重建比增量维护简单且足够快
            labTree.Reset(new Rectangle(0, 0, Screen.width * 0.5f, Screen.height * 0.5f), labCapacity);
            for (int i = 0; i < labPoints.Count; i++) labTree.Insert(new QuadPoint<int>(labPoints[i].x, labPoints[i].y, i));

            DrawTree(labTree, 3f, GridColor);

            var range = new Rectangle(mouseWorld.x, mouseWorld.y, labQueryHalf, labQueryHalf);
            labHits.Clear();
            labTree.Query(range, labHits);
            batch.RectOutline(range.X - range.W, range.Y - range.H, range.W * 2f, range.H * 2f, 2f, QueryColor);

            var hitSet = new HashSet<int>(labHits);
            for (int i = 0; i < labPoints.Count; i++)
            {
                Color32 c = i == hovered ? new Color32(255, 235, 59, 255)
                          : hitSet.Contains(i) ? QueryColor
                          : new Color32(0, 229, 255, 255);
                batch.Circle(labPoints[i], 5f, c);
            }
        }

        private int FindPoint(Vector2 p)
        {
            for (int i = 0; i < labPoints.Count; i++)
                if (Vector2.Distance(labPoints[i], p) < 20f) return i;
            return -1;
        }

        private void DrawTree<T>(QuadTree<T> tree, float thickness, Color32 color)
        {
            tree.Visit(node =>
            {
                Rectangle b = node.Boundary;
                batch.RectOutline(b.X - b.W, b.Y - b.H, b.W * 2f, b.H * 2f, thickness, color);
            });
        }

        // ---------------- 万鸟压测（原文阶段三） ----------------
        private void UpdateFlock(bool overPanel)
        {
            if (sim.Boids.Count != boidCount)
            {
                sim.Width = Screen.width;
                sim.Height = Screen.height;
                sim.Spawn(boidCount);
            }

            if (!overPanel && DemoInput.GetButtonDown(DemoInput.Left)) { mouseActive = true; mouseAttract = true; }
            if (!overPanel && DemoInput.GetButtonDown(DemoInput.Right)) { mouseActive = true; mouseAttract = false; }
            if (mouseActive && !DemoInput.GetButton(DemoInput.Left) && !DemoInput.GetButton(DemoInput.Right)) mouseActive = false;

            sim.Width = Screen.width;
            sim.Height = Screen.height;
            sim.Perception = perception;
            sim.Capacity = capacity;
            sim.UseQuadTree = useQuadTree;

            var sw = Stopwatch.StartNew();
            sim.Step(Mathf.Min(Time.deltaTime, 1f / 20f), mouseActive, mouseAttract, mouseWorld);
            logicMs = Mathf.Lerp(logicMs, (float)sw.Elapsed.TotalMilliseconds, 0.1f);

            if (showTree && sim.LastTree != null) DrawTree(sim.LastTree, 1f, new Color32(88, 166, 255, 46));

            var body = new Color32(120, 220, 255, 255);
            foreach (Boid b in sim.Boids)
            {
                Vector2 dir = b.Velocity.sqrMagnitude > 1e-6f ? b.Velocity.normalized : Vector2.up;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 p = b.Position;
                batch.Triangle(p + dir * 7f, p - dir * 4f + side * 3f, p - dir * 4f - side * 3f, body);
            }
            if (mouseActive)
            {
                if (mouseAttract) batch.Ring(mouseWorld, 20f, 2f, new Color32(120, 255, 160, 160));
                else batch.Ring(mouseWorld, sim.FleeRadius, 2f, new Color32(255, 90, 90, 160));
            }
        }

        private void OnGUI()
        {
            if (labTree == null) return;
            AlgorithmDemos.DemoPresentation.BeginGui();
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("四叉树 QuadTree");
            var newMode = (Mode)DemoGui.Choice((int)mode, ModeNames, true);
            if (newMode != mode)
            {
                mode = newMode;
                logicMs = 0f;
            }

            if (mode == Mode.Lab)
            {
                labCapacity = DemoGui.IntSlider("节点容量", labCapacity, 1, 10);
                labQueryHalf = DemoGui.Slider("查询半边长", labQueryHalf, 20f, 300f, "0");
                if (GUILayout.Button("重置为 10 个随机点")) ResetLab();
                GUILayout.Label("左键点空白处加点；按住已有的点拖拽。\n橙框是鼠标处的范围查询，命中的点变橙。");
            }
            else
            {
                boidCount = DemoGui.IntSlider("鸟的数量", boidCount, 100, 10000, 100);
                capacity = DemoGui.IntSlider("节点容量", capacity, 1, 32);
                perception = DemoGui.Slider("感知半径", perception, 10f, 150f, "0");
                bool newUse = DemoGui.Toggle("启用四叉树（关掉 = O(N²) 暴力遍历）", useQuadTree);
                if (newUse != useQuadTree)
                {
                    useQuadTree = newUse;
                    logicMs = 0f;
                }
                showTree = DemoGui.Toggle("绘制树网格", showTree);
                GUILayout.Label("左键按住吸引 / 右键按住驱散");
            }
            GUILayout.EndArea();

            if (mode == Mode.Lab)
            {
                DemoGui.HudLine($"实验室 | 点数 {labPoints.Count} | 树节点 {labTree.CountNodes()} | 查询命中 {labHits.Count} | 容量 {labCapacity}");
            }
            else
            {
                string tree = sim.LastTree != null ? $" | 树节点 {sim.LastTree.CountNodes()}" : "";
                DemoGui.HudLine($"{(useQuadTree ? "四叉树" : "暴力遍历 O(N²)")} | 鸟 {sim.Boids.Count} | FPS {fps:0} | 逻辑 {logicMs:0.00} ms{tree}");
            }
        }
    }
}
