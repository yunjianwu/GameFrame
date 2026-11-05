namespace Demos.FlowFieldPathfinding
{
    /// <summary>默认迷宫：三道“城墙”+ 窄门 + 三片沼泽，终点核心在右侧（与浏览器版完全相同）</summary>
    public static class FlowMap
    {
        public const int Cols = 48;
        public const int Rows = 27;
        public const int DefaultTargetX = 44;
        public const int DefaultTargetY = 13;

        private static void FillRect(FlowField ff, int x0, int y0, int x1, int y1, byte cost)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (ff.InBounds(x, y)) ff.Cost[x, y] = cost;
        }

        public static void BuildDefault(FlowField ff)
        {
            const byte wall = FlowField.CostWall;
            const byte swamp = FlowField.CostSwamp;
            const byte empty = FlowField.CostEmpty;

            ff.Fill(empty);

            FillRect(ff, 12, 0, 12, 26, wall);
            FillRect(ff, 12, 4, 12, 6, empty);
            FillRect(ff, 12, 20, 12, 22, empty);

            FillRect(ff, 24, 0, 24, 26, wall);
            FillRect(ff, 24, 12, 24, 14, empty);

            FillRect(ff, 36, 0, 36, 26, wall);
            FillRect(ff, 36, 2, 36, 3, empty);
            FillRect(ff, 36, 23, 36, 24, empty);

            FillRect(ff, 16, 9, 21, 9, wall);
            FillRect(ff, 28, 18, 33, 18, wall);

            FillRect(ff, 14, 13, 21, 18, swamp);
            FillRect(ff, 26, 4, 33, 10, swamp);
            FillRect(ff, 38, 10, 41, 16, swamp);

            ff.SetTarget(DefaultTargetX, DefaultTargetY);
        }
    }
}
