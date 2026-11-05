using System;

namespace Demos.FlowFieldPathfinding
{
    /// <summary>
    /// 对照组：传统 A*（8 方向、八方向距离启发、二叉堆，纯 C#）。
    /// 边权与流场完全一致：离开格子 a 走到 b 的代价 = Cost[a] × (1 或 √2)，同样禁止斜穿墙角，
    /// 所以求出的最短代价应当等于流场积分场在起点处的值。
    /// 缓冲区在多次搜索间复用（用代数标记代替清零），对 A* 已经是比较公平的实现。
    /// </summary>
    public sealed class AStar
    {
        public struct Result
        {
            public float Cost;
            public int Expanded;
        }

        private readonly FlowField ff;
        private readonly float[] g;
        private readonly int[] seen;
        private readonly int[] closed;
        private int[] heapIdx;
        private float[] heapF;
        private int heapCount;
        private int stamp;

        public AStar(FlowField ff)
        {
            this.ff = ff;
            int n = ff.Cols * ff.Rows;
            g = new float[n];
            seen = new int[n];
            closed = new int[n];
            heapIdx = new int[n * 2];
            heapF = new float[n * 2];
        }

        public Result Search(int sx, int sy, int tx, int ty)
        {
            stamp++;
            heapCount = 0;
            int rows = ff.Rows;
            byte[,] cost = ff.Cost;

            int start = sx * rows + sy;
            g[start] = 0f;
            seen[start] = stamp;
            Push(start, Heuristic(sx, sy, tx, ty));
            int expanded = 0;

            while (heapCount > 0)
            {
                int cur = Pop();
                if (closed[cur] == stamp) continue;
                closed[cur] = stamp;
                expanded++;
                int cx = cur / rows;
                int cy = cur % rows;
                if (cx == tx && cy == ty) return new Result { Cost = g[cur], Expanded = expanded };

                for (int i = 0; i < FlowField.Dirs8.Length; i++)
                {
                    GridDir d = FlowField.Dirs8[i];
                    int nx = cx + d.Dx;
                    int ny = cy + d.Dy;
                    if (!ff.InBounds(nx, ny) || cost[nx, ny] == FlowField.CostWall) continue;
                    if (ff.CutsCorner(cx, cy, d)) continue;
                    int ni = nx * rows + ny;
                    if (closed[ni] == stamp) continue;
                    float ng = g[cur] + cost[cx, cy] * d.DistMul;
                    if (seen[ni] != stamp || ng < g[ni])
                    {
                        seen[ni] = stamp;
                        g[ni] = ng;
                        Push(ni, ng + Heuristic(nx, ny, tx, ty));
                    }
                }
            }
            return new Result { Cost = float.PositiveInfinity, Expanded = expanded };
        }

        /// <summary>最小过路费为 1，八方向距离是可采纳且一致的启发函数</summary>
        private static float Heuristic(int x, int y, int tx, int ty)
        {
            int dx = Math.Abs(x - tx);
            int dy = Math.Abs(y - ty);
            return Math.Max(dx, dy) + 0.41421356f * Math.Min(dx, dy);
        }

        private void Push(int idx, float f)
        {
            if (heapCount == heapIdx.Length)
            {
                Array.Resize(ref heapIdx, heapCount * 2);
                Array.Resize(ref heapF, heapCount * 2);
            }
            int i = heapCount++;
            heapIdx[i] = idx;
            heapF[i] = f;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heapF[p] <= heapF[i]) break;
                Swap(p, i);
                i = p;
            }
        }

        private int Pop()
        {
            int top = heapIdx[0];
            heapCount--;
            if (heapCount > 0)
            {
                heapIdx[0] = heapIdx[heapCount];
                heapF[0] = heapF[heapCount];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1;
                    int r = l + 1;
                    int m = i;
                    if (l < heapCount && heapF[l] < heapF[m]) m = l;
                    if (r < heapCount && heapF[r] < heapF[m]) m = r;
                    if (m == i) break;
                    Swap(m, i);
                    i = m;
                }
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            (heapIdx[a], heapIdx[b]) = (heapIdx[b], heapIdx[a]);
            (heapF[a], heapF[b]) = (heapF[b], heapF[a]);
        }
    }
}
