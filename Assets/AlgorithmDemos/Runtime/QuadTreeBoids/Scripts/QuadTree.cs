using System.Collections.Generic;

namespace Demos.QuadTreeBoids
{
    /// <summary>以 (X, Y) 为中心、半宽 W、半高 H 的轴对齐矩形（与原文 Rectangle 同义）</summary>
    public readonly struct Rectangle
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public Rectangle(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public bool Contains(float px, float py)
        {
            return px >= X - W && px <= X + W && py >= Y - H && py <= Y + H;
        }

        public bool Intersects(in Rectangle range)
        {
            return !(range.X - range.W > X + W ||
                     range.X + range.W < X - W ||
                     range.Y - range.H > Y + H ||
                     range.Y + range.H < Y - H);
        }
    }

    public readonly struct QuadPoint<T>
    {
        public readonly float X;
        public readonly float Y;
        public readonly T Data;

        public QuadPoint(float x, float y, T data)
        {
            X = x;
            Y = y;
            Data = data;
        }
    }

    /// <summary>
    /// 点数超过 Capacity 就一分为四。原文每帧 new 一棵新树；这里改为 Reset 后复用节点对象，
    /// 逻辑完全相同，只是避免每帧上千个节点的 GC 分配。
    /// 重合点在最大深度处保留在同一个叶节点，避免交互拖拽时无限分裂。
    /// </summary>
    public sealed class QuadTree<T>
    {
        private const int MaximumDepth = 16;
        private readonly int depth;
        public Rectangle Boundary { get; private set; }
        public int Capacity { get; private set; }
        public bool Divided { get; private set; }
        public readonly List<QuadPoint<T>> Points = new List<QuadPoint<T>>(4);

        public QuadTree<T> NorthEast { get; private set; }
        public QuadTree<T> NorthWest { get; private set; }
        public QuadTree<T> SouthEast { get; private set; }
        public QuadTree<T> SouthWest { get; private set; }

        public QuadTree(Rectangle boundary, int capacity) : this(boundary, capacity, 0) { }

        private QuadTree(Rectangle boundary, int capacity, int currentDepth)
        {
            depth = currentDepth;
            Reset(boundary, capacity);
        }

        /// <summary>清空为一个未分裂的根节点；子节点对象保留，下次分裂时复用</summary>
        public void Reset(Rectangle boundary, int capacity)
        {
            if (capacity < 1) throw new System.ArgumentOutOfRangeException(nameof(capacity));
            Boundary = boundary;
            Capacity = capacity;
            Points.Clear();
            Divided = false;
        }

        private void Subdivide()
        {
            float x = Boundary.X;
            float y = Boundary.Y;
            float w = Boundary.W / 2f;
            float h = Boundary.H / 2f;
            NorthEast = Child(NorthEast, new Rectangle(x + w, y + h, w, h));
            NorthWest = Child(NorthWest, new Rectangle(x - w, y + h, w, h));
            SouthEast = Child(SouthEast, new Rectangle(x + w, y - h, w, h));
            SouthWest = Child(SouthWest, new Rectangle(x - w, y - h, w, h));
            Divided = true;
        }

        private QuadTree<T> Child(QuadTree<T> existing, Rectangle r)
        {
            if (existing == null) return new QuadTree<T>(r, Capacity, depth + 1);
            existing.Reset(r, Capacity);
            return existing;
        }

        public bool Insert(in QuadPoint<T> point)
        {
            if (!Boundary.Contains(point.X, point.Y)) return false;

            if (Points.Count < Capacity || depth >= MaximumDepth || Boundary.W <= 0.0001f || Boundary.H <= 0.0001f)
            {
                Points.Add(point);
                return true;
            }

            if (!Divided) Subdivide();

            return NorthEast.Insert(point) ||
                   NorthWest.Insert(point) ||
                   SouthEast.Insert(point) ||
                   SouthWest.Insert(point);
        }

        /// <summary>与 range 不相交的子树整棵跳过——这就是性能来源。命中的点的 Data 追加到 found</summary>
        public void Query(in Rectangle range, List<T> found)
        {
            if (!Boundary.Intersects(range)) return;

            for (int i = 0; i < Points.Count; i++)
            {
                QuadPoint<T> p = Points[i];
                if (range.Contains(p.X, p.Y)) found.Add(p.Data);
            }

            if (Divided)
            {
                NorthWest.Query(range, found);
                NorthEast.Query(range, found);
                SouthWest.Query(range, found);
                SouthEast.Query(range, found);
            }
        }

        public int CountNodes()
        {
            if (!Divided) return 1;
            return 1 + NorthEast.CountNodes() + NorthWest.CountNodes() + SouthEast.CountNodes() + SouthWest.CountNodes();
        }

        /// <summary>深度优先遍历所有节点（渲染网格用）</summary>
        public void Visit(System.Action<QuadTree<T>> visitor)
        {
            visitor(this);
            if (!Divided) return;
            NorthEast.Visit(visitor);
            NorthWest.Visit(visitor);
            SouthEast.Visit(visitor);
            SouthWest.Visit(visitor);
        }
    }
}
