using System.Collections.Generic;
using UnityEngine;

namespace Demos.BoidsFlocking
{
    /// <summary>对应原文 FlockManager.update 的纯逻辑部分：先统一算受力，再统一移动，最后穿屏</summary>
    public sealed class FlockSimulation
    {
        public const float WrapMargin = 50f;

        public readonly List<Boid> Boids = new List<Boid>();
        /// <summary>本帧发生穿屏的鸟的下标，渲染层据此切断拖尾</summary>
        public readonly List<int> WrappedThisFrame = new List<int>();

        public float Width;
        public float Height;
        public float Perception = 50f;
        public FlockWeights Weights = new FlockWeights(1.5f, 1f, 1f);
        public float AttractScale = 2f;
        public float FleeRadius = 300f;

        private readonly System.Random rng;

        public FlockSimulation(float width, float height, int seed)
        {
            Width = width;
            Height = height;
            rng = new System.Random(seed);
        }

        public void Spawn(int count)
        {
            Boids.Clear();
            for (int i = 0; i < count; i++)
            {
                var b = new Boid(rng);
                b.Position = new Vector2((float)(rng.NextDouble() - 0.5) * Width, (float)(rng.NextDouble() - 0.5) * Height);
                Boids.Add(b);
            }
        }

        /// <param name="mouseActive">鼠标是否按下</param>
        /// <param name="attract">true = 左键吸引，false = 右键驱散</param>
        public void Step(float dt, bool mouseActive, bool attract, Vector2 mouse)
        {
            for (int i = 0; i < Boids.Count; i++)
            {
                Boid boid = Boids[i];
                boid.Flock(Boids, Perception, Weights);
                if (mouseActive)
                {
                    boid.Acceleration += attract ? boid.Seek(mouse) * AttractScale : boid.Flee(mouse, FleeRadius);
                }
            }

            WrappedThisFrame.Clear();
            float halfW = Width * 0.5f + WrapMargin;
            float halfH = Height * 0.5f + WrapMargin;
            for (int i = 0; i < Boids.Count; i++)
            {
                Boids[i].Integrate(dt);
                if (Boids[i].Wrap(halfW, halfH)) WrappedThisFrame.Add(i);
            }
        }

        /// <summary>速度方向一致度：1 = 全体同向，接近 0 = 完全随机</summary>
        public float Polarization()
        {
            if (Boids.Count == 0) return 0f;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < Boids.Count; i++) sum += Boids[i].Velocity.normalized;
            return sum.magnitude / Boids.Count;
        }
    }
}
