using System;
using System.Collections.Generic;

namespace Demos.FlowFieldPathfinding
{
    public sealed class Agent
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public bool InSwamp;
    }

    /// <summary>
    /// 第四、五步：怪物 O(1) 查表移动 + 九宫格分桶排斥（纯 C#）。
    /// 坐标为“地图像素”：原点在地图左上角、Y 向下，格子 (c, r) 覆盖 [c*CellSize, (c+1)*CellSize) × [r*CellSize, (r+1)*CellSize)。
    /// </summary>
    public sealed class Crowd
    {
        public readonly List<Agent> Agents = new List<Agent>();

        /// <summary>每帧最大位移（像素，按 60 FPS 计）</summary>
        public float MaxSpeed = 1.6f;
        public bool Separation;
        public float MinSepDist = 6f;
        public float SepWeight = 1.35f;

        public float CellSize { get; private set; }

        private readonly FlowField ff;
        private readonly List<Agent>[,] buckets;
        private readonly Random rng;

        public Crowd(FlowField ff, float cellSize, int seed)
        {
            this.ff = ff;
            CellSize = cellSize;
            rng = new Random(seed);
            buckets = new List<Agent>[ff.Cols, ff.Rows];
            for (int x = 0; x < ff.Cols; x++)
                for (int y = 0; y < ff.Rows; y++)
                    buckets[x, y] = new List<Agent>(8);
        }

        /// <summary>窗口缩放导致格子像素变化时，按比例缩放所有怪物坐标</summary>
        public void Rescale(float newCellSize)
        {
            float s = newCellSize / CellSize;
            foreach (var a in Agents)
            {
                a.X *= s;
                a.Y *= s;
            }
            CellSize = newCellSize;
        }

        public int CellX(float px) => (int)Math.Floor(px / CellSize);
        public int CellY(float py) => (int)Math.Floor(py / CellSize);

        public bool HitWall(float px, float py) => ff.IsWall(CellX(px), CellY(py));

        /// <summary>在可达的随机空地格子里生成；minCol..maxCol 限定出生区域</summary>
        public int SpawnRandom(int count, int minCol = 0, int maxCol = -1)
        {
            if (maxCol < 0) maxCol = ff.Cols - 1;
            int spawned = 0;
            for (int attempt = 0; spawned < count && attempt < count * 50; attempt++)
            {
                int c = minCol + rng.Next(maxCol - minCol + 1);
                int r = rng.Next(ff.Rows);
                float integ = ff.Integration[c, r];
                if (ff.IsWall(c, r) || float.IsInfinity(integ) || integ == 0f) continue;
                Agents.Add(new Agent
                {
                    X = (c + 0.1f + (float)rng.NextDouble() * 0.8f) * CellSize,
                    Y = (r + 0.1f + (float)rng.NextDouble() * 0.8f) * CellSize,
                });
                spawned++;
            }
            return spawned;
        }

        /// <summary>
        /// 推进一帧，返回本帧抵达终点格的数量（这些怪已从 Agents 移除）。
        /// 被新画的墙压住的怪物也会被移除。
        /// </summary>
        public int Step(float dt)
        {
            float frameScale = dt * 60f;

            if (Separation)
            {
                foreach (var b in buckets) b.Clear();
                foreach (var a in Agents)
                {
                    int c = CellX(a.X), r = CellY(a.Y);
                    if (ff.InBounds(c, r)) buckets[c, r].Add(a);
                }
            }

            int arrived = 0;
            int write = 0;
            for (int i = 0; i < Agents.Count; i++)
            {
                Agent agent = Agents[i];
                int c = CellX(agent.X);
                int r = CellY(agent.Y);
                if (ff.IsWall(c, r)) continue;
                if (c == ff.TargetX && r == ff.TargetY)
                {
                    arrived++;
                    continue;
                }

                FlowVector flow = ff.Vectors[c, r];
                float steerX = flow.X;
                float steerY = flow.Y;

                if (Separation)
                {
                    float sepX = 0f, sepY = 0f;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int bx = c + dx, by = r + dy;
                            if (!ff.InBounds(bx, by)) continue;
                            List<Agent> bucket = buckets[bx, by];
                            for (int k = 0; k < bucket.Count; k++)
                            {
                                Agent other = bucket[k];
                                if (other == agent) continue;
                                float ox = agent.X - other.X;
                                float oy = agent.Y - other.Y;
                                float dist = (float)Math.Sqrt(ox * ox + oy * oy);
                                if (dist > 0f && dist < MinSepDist)
                                {
                                    float push = (MinSepDist - dist) / MinSepDist;
                                    sepX += ox / dist * push;
                                    sepY += oy / dist * push;
                                }
                            }
                        }
                    }
                    steerX += sepX * SepWeight;
                    steerY += sepY * SepWeight;
                }

                agent.InSwamp = ff.Cost[c, r] == FlowField.CostSwamp;
                float speed = (agent.InSwamp ? 0.45f : 1f) * MaxSpeed;
                agent.VX = agent.VX * 0.8f + steerX * speed * 0.2f;
                agent.VY = agent.VY * 0.8f + steerY * speed * 0.2f;

                // X / Y 分轴检测：贴墙时沿墙面滑动而不是卡住
                float mx = agent.VX * frameScale;
                float my = agent.VY * frameScale;
                if (!HitWall(agent.X + mx, agent.Y)) agent.X += mx;
                else agent.VX = 0f;
                if (!HitWall(agent.X, agent.Y + my)) agent.Y += my;
                else agent.VY = 0f;

                Agents[write++] = agent;
            }
            Agents.RemoveRange(write, Agents.Count - write);
            return arrived;
        }
    }
}
