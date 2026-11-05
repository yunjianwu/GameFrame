using System.Diagnostics;
using UnityEngine;

namespace Demos.BoidsFlocking
{
    /// <summary>
    /// Boids 鸟群演示，由 GF 菜单加载对应业务场景后创建。
    /// 左键按住吸引，右键按住驱散；左上角面板切换文章的四个阶段并调参。
    /// </summary>
    public sealed class BoidsDemo : MonoBehaviour
    {
        private static readonly string[] StageNames =
        {
            "阶段一：只会乱飞",
            "阶段二：+分离",
            "阶段三：+对齐",
            "阶段四：+凝聚（完整 Boids）",
        };

        private static readonly FlockWeights[] StageWeights =
        {
            new FlockWeights(0f, 0f, 0f),
            new FlockWeights(1.5f, 0f, 0f),
            new FlockWeights(1.5f, 1f, 0f),
            new FlockWeights(1.5f, 1f, 1f),
        };

        private const int TrailLength = 8;

        [SerializeField] private int boidCount = 100;
        [SerializeField] private float perception = 50f;
        [SerializeField] private int stage = 3;
        [SerializeField] private bool trails = true;
        [SerializeField] private int seed = 12345;

        private FlockSimulation sim;
        private ShapeBatch batch;
        private Camera cam;
        private Vector2[] trailBuffer = new Vector2[0];
        private int trailHead;

        private Rect panelRect = new Rect(12, 12, 320, 330);
        private bool mouseActive;
        private bool mouseAttract;
        private Vector2 mouseWorld;
        private float fps = 60f;
        private float logicMs;

        public FlockSimulation Simulation => sim;

        private void Start()
        {
            cam = DemoCamera.Setup(gameObject, new Color32(11, 16, 32, 255));
            batch = new ShapeBatch("BoidsBatch");
            sim = new FlockSimulation(Screen.width, Screen.height, seed);
            sim.Perception = perception;
            sim.Weights = StageWeights[stage];
            Respawn();
        }

        private void OnDestroy()
        {
            batch?.Destroy();
        }

        private void Respawn()
        {
            sim.Spawn(boidCount);
            trailBuffer = new Vector2[boidCount * TrailLength];
            for (int i = 0; i < boidCount; i++) ResetTrail(i);
        }

        private void ResetTrail(int i)
        {
            for (int k = 0; k < TrailLength; k++) trailBuffer[i * TrailLength + k] = sim.Boids[i].Position;
        }

        private void Update()
        {
            DemoCamera.Fit(cam);
            sim.Width = Screen.width;
            sim.Height = Screen.height;

            HandleInput();

            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

            var sw = Stopwatch.StartNew();
            sim.Perception = perception;
            sim.Step(dt, mouseActive, mouseAttract, mouseWorld);
            logicMs = Mathf.Lerp(logicMs, (float)sw.Elapsed.TotalMilliseconds, 0.1f);

            trailHead = (trailHead + 1) % TrailLength;
            for (int i = 0; i < sim.Boids.Count; i++) trailBuffer[i * TrailLength + trailHead] = sim.Boids[i].Position;
            // 穿屏后切断拖尾，否则会画出横穿屏幕的线（对应原文 MotionStreak.reset）
            foreach (int i in sim.WrappedThisFrame) ResetTrail(i);

            Render();
        }

        private void HandleInput()
        {
            bool overPanel = panelRect.Contains(DemoInput.GuiMousePosition);
            mouseWorld = DemoCamera.ScreenToWorld(DemoInput.MousePosition);
            if (!overPanel && DemoInput.GetButtonDown(DemoInput.Left)) { mouseActive = true; mouseAttract = true; }
            if (!overPanel && DemoInput.GetButtonDown(DemoInput.Right)) { mouseActive = true; mouseAttract = false; }
            if (mouseActive && !DemoInput.GetButton(DemoInput.Left) && !DemoInput.GetButton(DemoInput.Right)) mouseActive = false;
        }

        private void Render()
        {
            batch.Clear();
            var body = new Color32(120, 220, 255, 255);

            if (trails)
            {
                for (int i = 0; i < sim.Boids.Count; i++)
                {
                    int baseIdx = i * TrailLength;
                    for (int k = 1; k < TrailLength; k++)
                    {
                        int a = baseIdx + (trailHead - k + 1 + TrailLength) % TrailLength;
                        int b = baseIdx + (trailHead - k + TrailLength) % TrailLength;
                        byte alpha = (byte)(110 * (TrailLength - k) / TrailLength);
                        batch.Line(trailBuffer[a], trailBuffer[b], 3f * (TrailLength - k) / TrailLength + 0.5f, new Color32(120, 220, 255, alpha));
                    }
                }
            }

            for (int i = 0; i < sim.Boids.Count; i++)
            {
                Boid b = sim.Boids[i];
                Vector2 dir = b.Velocity.sqrMagnitude > 1e-6f ? b.Velocity.normalized : Vector2.up;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 p = b.Position;
                batch.Triangle(p + dir * 10f, p - dir * 6f + side * 4.5f, p - dir * 6f - side * 4.5f, body);
            }

            if (mouseActive)
            {
                if (mouseAttract) batch.Ring(mouseWorld, 20f, 2f, new Color32(120, 255, 160, 160));
                else batch.Ring(mouseWorld, sim.FleeRadius, 2f, new Color32(255, 90, 90, 160));
            }

            batch.Draw(cam);
        }

        private void OnGUI()
        {
            if (sim == null) return;
            AlgorithmDemos.DemoPresentation.BeginGui();
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Boids 鸟群（分离 / 对齐 / 凝聚）");
            int newStage = DemoGui.Choice(stage, StageNames);
            if (newStage != stage)
            {
                stage = newStage;
                sim.Weights = StageWeights[stage];
            }

            int newCount = DemoGui.IntSlider("数量", boidCount, 10, 1500, 10);
            if (newCount != boidCount)
            {
                boidCount = newCount;
                Respawn();
            }
            perception = DemoGui.Slider("感知半径", perception, 10f, 200f, "0");
            var w = sim.Weights;
            w.Sep = DemoGui.Slider("分离权重", w.Sep, 0f, 5f);
            w.Ali = DemoGui.Slider("对齐权重", w.Ali, 0f, 5f);
            w.Coh = DemoGui.Slider("凝聚权重", w.Coh, 0f, 5f);
            sim.Weights = w;
            trails = DemoGui.Toggle("拖尾", trails);
            GUILayout.Label("左键按住：吸引鸟群；右键按住：300px 内四散逃离");
            GUILayout.EndArea();

            DemoGui.HudLine($"FPS {fps:0} | 逻辑 {logicMs:0.00} ms | 鸟 {sim.Boids.Count} | 方向一致度 {sim.Polarization():0.00}");
        }
    }
}
