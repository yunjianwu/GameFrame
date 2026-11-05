using System;
using System.Collections.Generic;

namespace Demos.FlowFieldPathfinding
{
    public readonly struct GridDir
    {
        public readonly int Dx;
        public readonly int Dy;
        public readonly float DistMul;

        public GridDir(int dx, int dy, float distMul)
        {
            Dx = dx;
            Dy = dy;
            DistMul = distMul;
        }

        public bool IsDiagonal => Dx != 0 && Dy != 0;
    }

    public struct FlowVector
    {
        public float X;
        public float Y;

        public bool IsZero => X == 0f && Y == 0f;
    }

    /// <summary>
    /// 三层网格场（纯 C#，不依赖 UnityEngine），全部按 [x, y] 存放，y = 0 为地图最上面一行：
    /// Cost 过路费 → Integration 到终点的累计代价 → Vectors 每格的单位方向。
    /// </summary>
    public sealed class FlowField
    {
        public const byte CostEmpty = 1;
        public const byte CostSwamp = 5;
        public const byte CostWall = 255;

        public static readonly GridDir[] Dirs8 =
        {
            new GridDir(1, 0, 1f),
            new GridDir(-1, 0, 1f),
            new GridDir(0, 1, 1f),
            new GridDir(0, -1, 1f),
            new GridDir(1, 1, 1.41421356f),
            new GridDir(1, -1, 1.41421356f),
            new GridDir(-1, 1, 1.41421356f),
            new GridDir(-1, -1, 1.41421356f),
        };

        public readonly int Cols;
        public readonly int Rows;
        public readonly byte[,] Cost;
        public readonly float[,] Integration;
        public readonly FlowVector[,] Vectors;

        public int TargetX { get; private set; }
        public int TargetY { get; private set; }

        private readonly List<int> queue;

        public FlowField(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
            Cost = new byte[cols, rows];
            Integration = new float[cols, rows];
            Vectors = new FlowVector[cols, rows];
            queue = new List<int>(cols * rows * 2);
            Fill(CostEmpty);
        }

        public void Fill(byte cost)
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Cost[x, y] = cost;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Cols && y < Rows;

        public bool IsWall(int x, int y) => !InBounds(x, y) || Cost[x, y] == CostWall;

        /// <summary>斜向移动时两侧正交格只要有一面墙就禁止，防止斜穿墙角</summary>
        public bool CutsCorner(int x, int y, in GridDir d)
        {
            if (!d.IsDiagonal) return false;
            return Cost[x + d.Dx, y] == CostWall || Cost[x, y + d.Dy] == CostWall;
        }

        public void SetTarget(int x, int y)
        {
            TargetX = x;
            TargetY = y;
            Rebuild();
        }

        public void Rebuild()
        {
            BuildIntegrationField(TargetX, TargetY);
            BuildVectorField();
        }

        /// <summary>
        /// 从终点“倒水”向外扩散。找到更便宜的路就更新并重新入队（标号修正法），
        /// 所以代价不均匀（沼泽 5、斜向 ×√2）时结果仍是最短路。
        /// </summary>
        public void BuildIntegrationField(int targetX, int targetY)
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Integration[x, y] = float.PositiveInfinity;
            if (!InBounds(targetX, targetY) || Cost[targetX, targetY] == CostWall) return;

            Integration[targetX, targetY] = 0f;
            queue.Clear();
            queue.Add(targetX * Rows + targetY);
            int head = 0;
            while (head < queue.Count)
            {
                int cur = queue[head++];
                int cx = cur / Rows;
                int cy = cur % Rows;
                float curDist = Integration[cx, cy];
                for (int i = 0; i < Dirs8.Length; i++)
                {
                    GridDir d = Dirs8[i];
                    int nx = cx + d.Dx;
                    int ny = cy + d.Dy;
                    if (!InBounds(nx, ny) || Cost[nx, ny] == CostWall) continue;
                    if (CutsCorner(cx, cy, d)) continue;
                    float newDist = curDist + Cost[nx, ny] * d.DistMul;
                    if (newDist < Integration[nx, ny])
                    {
                        Integration[nx, ny] = newDist;
                        queue.Add(nx * Rows + ny);
                    }
                }
            }
        }

        /// <summary>每格指向 8 邻居里积分最小的那个；到不了终点的格子保持零向量</summary>
        public void BuildVectorField()
        {
            for (int x = 0; x < Cols; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    Vectors[x, y] = default;
                    if (Cost[x, y] == CostWall) continue;

                    float minDist = Integration[x, y];
                    int bestDx = 0;
                    int bestDy = 0;
                    for (int i = 0; i < Dirs8.Length; i++)
                    {
                        GridDir d = Dirs8[i];
                        int nx = x + d.Dx;
                        int ny = y + d.Dy;
                        if (!InBounds(nx, ny) || Cost[nx, ny] == CostWall) continue;
                        if (CutsCorner(x, y, d)) continue;
                        if (Integration[nx, ny] < minDist)
                        {
                            minDist = Integration[nx, ny];
                            bestDx = d.Dx;
                            bestDy = d.Dy;
                        }
                    }
                    if (bestDx != 0 || bestDy != 0)
                    {
                        float len = (float)Math.Sqrt(bestDx * bestDx + bestDy * bestDy);
                        Vectors[x, y] = new FlowVector { X = bestDx / len, Y = bestDy / len };
                    }
                }
            }
        }
    }
}
