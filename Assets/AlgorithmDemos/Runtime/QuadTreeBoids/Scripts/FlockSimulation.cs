using System.Collections.Generic;
using UnityEngine;

namespace Demos.QuadTreeBoids
{
    /// <summary>
    /// 上一期 FlockSimulation + 四叉树：UseQuadTree = false 时每只鸟遍历全体（O(N²)），
    /// true 时每帧重建四叉树，只把感知范围内的邻居交给 Boid.Flock。Boid 内部仍按圆形距离过滤，所以两种方式结果一致。
    /// </summary>
    public sealed class FlockSimulation
    {
        public const float WrapMargin = 50f;

        public readonly List<Boid> Boids = new List<Boid>();
        public readonly List<int> WrappedThisFrame = new List<int>();

        public float Width;
        public float Height;
        public float Perception = 50f;
        public FlockWeights Weights = new FlockWeights(1.5f, 1f, 1f);
        public float AttractScale = 2f;
        public float FleeRadius = 300f;
        public bool UseQuadTree = true;
        public int Capacity = 4;

        /// <summary>最近一帧构建的四叉树（UseQuadTree = false 时为 null）</summary>
        public QuadTree<Boid> LastTree { get; private set; }

        private readonly System.Random rng;
        private readonly QuadTree<Boid> tree = new QuadTree<Boid>(new Rectangle(0, 0, 1, 1), 4);
        private readonly List<Boid> neighbors = new List<Boid>(64);

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

        /// <summary>把另一套模拟的位置与速度原样拷过来（对比测试用）</summary>
        public void CopyStateFrom(FlockSimulation other)
        {
            Boids.Clear();
            foreach (var src in other.Boids)
            {
                var b = new Boid(rng) { Position = src.Position, Velocity = src.Velocity };
                Boids.Add(b);
            }
        }

        public void Step(float dt, bool mouseActive, bool attract, Vector2 mouse)
        {
            float halfW = Width * 0.5f + WrapMargin;
            float halfH = Height * 0.5f + WrapMargin;

            LastTree = null;
            if (UseQuadTree)
            {
                // 边界要覆盖穿屏缓冲区，否则缓冲区里的鸟插不进树、会丢邻居
                tree.Reset(new Rectangle(0, 0, halfW, halfH), Capacity);
                for (int i = 0; i < Boids.Count; i++)
                {
                    Boid b = Boids[i];
                    tree.Insert(new QuadPoint<Boid>(b.Position.x, b.Position.y, b));
                }
                LastTree = tree;
            }

            for (int i = 0; i < Boids.Count; i++)
            {
                Boid boid = Boids[i];
                List<Boid> candidates = Boids;
                if (UseQuadTree)
                {
                    neighbors.Clear();
                    tree.Query(new Rectangle(boid.Position.x, boid.Position.y, Perception, Perception), neighbors);
                    candidates = neighbors;
                }
                boid.Flock(candidates, Perception, Weights);
                if (mouseActive)
                {
                    boid.Acceleration += attract ? boid.Seek(mouse) * AttractScale : boid.Flee(mouse, FleeRadius);
                }
            }

            WrappedThisFrame.Clear();
            for (int i = 0; i < Boids.Count; i++)
            {
                Boids[i].Integrate(dt);
                if (Boids[i].Wrap(halfW, halfH)) WrappedThisFrame.Add(i);
            }
        }
    }
}
