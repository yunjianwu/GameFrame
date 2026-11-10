using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AlgorithmDemos;
using Demos.FlowFieldPathfinding;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace AlgorithmDemos.NavMeshExample
{
    public sealed partial class NavMeshPackageStress
    {
        private enum TerrainKind : byte { Normal, Wall, Swamp, Road }
        private enum TerrainBrush { Wall, Swamp, Road, Erase }

        private struct TerrainRectangle
        {
            public int X, Z, Width, Depth;
            public TerrainKind Kind;
            public Vector3 Center => new Vector3(X - 50f + Width * 0.5f, 0, Z - 30f + Depth * 0.5f);
        }

        private const byte NormalTerrainCost = 3;
        private const byte SwampTerrainCost = 6;
        private const byte RoadTerrainCost = 2;
        private const int SwampArea = 3;
        private const int RoadArea = 4;
        private readonly TerrainKind[,] _terrain = new TerrainKind[GridWidth, GridHeight];
        private readonly List<TerrainRectangle> _terrainRectangles = new List<TerrainRectangle>();
        private readonly Mesh[] _terrainMeshes = new Mesh[3];
        private readonly bool[] _unitOrderAccepted = new bool[UnitCount];
        private readonly bool[] _terrainHeldUnits = new bool[UnitCount];
        private TerrainKind[,] _navAppliedTerrain;
        private TerrainKind[,] _navBuildingTerrain;
        private int _terrainRectanglesRevision = -1;
        private bool _terrainInitialized;
        private bool _terrainDirty;
        private bool _terrainRebuilding;
        private bool _hasWeightedTerrain;
        private bool _paintingTerrain;
        private bool _hasPaintAnchor;
        private Vector2Int _paintAnchor;
        private TerrainBrush _terrainBrush;
        private int _terrainBrushRadius = 2;
        private float _nextTerrainCommit;
        private AsyncOperation _terrainNavUpdate;
        private Vector3[] _terrainCubeVertices;
        private int[] _terrainCubeTriangles;
        private string _terrainStatus = "左键拖动涂地图，右键下达移动命令";

        private Rect TerrainPanel => new Rect(Mathf.Max(430, Screen.width - 310), Mathf.Max(352, Screen.height - 274), 298, 262);
        public int TerrainRevision { get; private set; }
        public int NavigationRevision { get; private set; }
        public bool TerrainBusy => _terrainDirty || _terrainRebuilding;
        public string TerrainBrushName => _terrainBrush.ToString();

        private void EnsureTerrainMap()
        {
            if (!_terrainInitialized) FillDefaultTerrain();
            BuildTerrainRectangles();
        }

        private void FillDefaultTerrain()
        {
            Array.Clear(_terrain, 0, _terrain.Length);
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                {
                    Vector3 cell = CellCenter(x, z) - Offset;
                    foreach (var wall in Walls)
                        if (Mathf.Abs(cell.x - wall.center.x) < wall.size.x * 0.5f &&
                            Mathf.Abs(cell.z - wall.center.z) < wall.size.z * 0.5f)
                        {
                            _terrain[x, z] = TerrainKind.Wall;
                            break;
                        }
                }
            _terrainInitialized = true;
        }

        public void ResetTerrain()
        {
            if (_preparing || !IsRunning) return;
            FillDefaultTerrain();
            TerrainRevision++;
            _terrainStatus = "已恢复初始地图并重新布置单位";
            BeginStress();
        }

        public void SetTerrainBrush(string name)
        {
            if (Enum.TryParse(name, out TerrainBrush brush) && Enum.IsDefined(typeof(TerrainBrush), brush))
                _terrainBrush = brush;
        }

        public string TerrainKindAt(Vector3 world)
        {
            return WorldToCell(world, out int x, out int z) ? _terrain[x, z].ToString() : "Outside";
        }

        public float TerrainSpeedAt(Vector3 world)
        {
            if (!WorldToCell(world, out int x, out int z)) return 1;
            return _terrain[x, z] == TerrainKind.Swamp ? 0.5f : _terrain[x, z] == TerrainKind.Road ? 1.5f : 1;
        }

        public int PaintTerrain(Vector3 world, string name, int radius)
        {
            if (!IsRunning || _preparing || !IsFinite(world.x) || !IsFinite(world.z) ||
                !WorldToCell(world, out int cx, out int cz)) return 0;
            TerrainKind kind;
            if (name == "Erase") kind = TerrainKind.Normal;
            else if (!Enum.TryParse(name, out kind) || !Enum.IsDefined(typeof(TerrainKind), kind)) return 0;
            radius = Mathf.Clamp(radius, 0, 5);
            int changed = 0;
            int protectedCells = 0;
            for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(GridHeight - 1, cz + radius); z++)
                for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(GridWidth - 1, cx + radius); x++)
                {
                    if (_terrain[x, z] == kind) continue;
                    if (kind == TerrainKind.Wall && IsTerrainCellProtected(x, z))
                    {
                        protectedCells++;
                        continue;
                    }
                    _terrain[x, z] = kind;
                    changed++;
                }
            if (changed > 0)
            {
                TerrainRevision++;
                if (!_terrainDirty) _nextTerrainCommit = Time.realtimeSinceStartup + 0.1f;
                _terrainDirty = true;
                _terrainStatus = "地图已修改，正在更新路线";
            }
            else if (protectedCells > 0) _terrainStatus = "单位和目的地附近保留通行空间";
            return changed;
        }

        private bool IsTerrainCellProtected(int x, int z)
        {
            Vector3 center = CellCenter(x, z);
            // 给烘焙时的半径收缩也留出余量，避免新增墙体让现有 Agent 脱离导航网格。
            foreach (Transform unit in _unitTransforms)
            {
                float dx = Mathf.Max(0, Mathf.Abs(unit.position.x - center.x) - 0.5f);
                float dz = Mathf.Max(0, Mathf.Abs(unit.position.z - center.z) - 0.5f);
                if (dx * dx + dz * dz < 0.8f * 0.8f) return true;
            }
            return OrderSequence > 0 && Mathf.Abs(Destination.x - center.x) <= 1.5f && Mathf.Abs(Destination.z - center.z) <= 1.5f;
        }

        private bool IsTerrainPointerBlocked(Vector2 screen)
        {
            Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
            return Panel.Contains(gui) || TerrainPanel.Contains(gui);
        }

        private bool TryTerrainScreenPoint(Vector2 screen, out Vector3 world)
        {
            world = default;
            if (!IsRunning || _stressCamera == null || screen.x < 0 || screen.x >= Screen.width || screen.y < 0 ||
                screen.y >= Screen.height || DemoInputGate.IsBlocked(screen) || IsTerrainPointerBlocked(screen)) return false;
            Ray ray = _stressCamera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return WorldToCell(world, out _, out _);
        }

        public bool TryPaintTerrainScreen(Vector2 screen)
        {
            return TryTerrainScreenPoint(screen, out Vector3 world) && PaintTerrain(world, _terrainBrush.ToString(), _terrainBrushRadius) > 0;
        }

        private void HandleTerrainInput()
        {
            if (Input.GetMouseButtonUp(0)) { _paintingTerrain = false; _hasPaintAnchor = false; }
            if (Input.GetMouseButtonDown(0))
            {
                _paintingTerrain = TryTerrainScreenPoint(Input.mousePosition, out _);
                _hasPaintAnchor = false;
            }
            if (!_paintingTerrain || !Input.GetMouseButton(0)) return;
            if (!TryTerrainScreenPoint(Input.mousePosition, out Vector3 world)) { _hasPaintAnchor = false; return; }
            WorldToCell(world, out int x, out int z);
            var cell = new Vector2Int(x, z);
            Vector2Int from = _hasPaintAnchor ? _paintAnchor : cell;
            int steps = Mathf.Max(Mathf.Abs(cell.x - from.x), Mathf.Abs(cell.y - from.y), 1);
            // 补齐鼠标两帧之间经过的格子，快速拖动时墙体也不会断开。
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int px = Mathf.RoundToInt(Mathf.Lerp(from.x, cell.x, t));
                int pz = Mathf.RoundToInt(Mathf.Lerp(from.y, cell.y, t));
                PaintTerrain(CellCenter(px, pz), _terrainBrush.ToString(), _terrainBrushRadius);
            }
            _paintAnchor = cell;
            _hasPaintAnchor = true;
        }

        private void DrawTerrainControls()
        {
            bool enabled = GUI.enabled;
            GUI.enabled = IsRunning && !_preparing;
            GUILayout.BeginArea(TerrainPanel, GUI.skin.box);
            GUILayout.Label("动态地图  /  " + (_terrainBrush == TerrainBrush.Wall ? "障碍物" :
                _terrainBrush == TerrainBrush.Swamp ? "沼泽" : _terrainBrush == TerrainBrush.Road ? "道路" : "擦除"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("障碍物", GUILayout.Height(29))) SetTerrainBrush("Wall");
            if (GUILayout.Button("擦除", GUILayout.Height(29))) SetTerrainBrush("Erase");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("沼泽 ×0.5", GUILayout.Height(29))) SetTerrainBrush("Swamp");
            if (GUILayout.Button("道路 ×1.5", GUILayout.Height(29))) SetTerrainBrush("Road");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("画笔", GUILayout.Width(34));
            if (GUILayout.Toggle(_terrainBrushRadius == 0, "1 格", GUI.skin.button)) _terrainBrushRadius = 0;
            if (GUILayout.Toggle(_terrainBrushRadius == 2, "5 格", GUI.skin.button)) _terrainBrushRadius = 2;
            if (GUILayout.Toggle(_terrainBrushRadius == 5, "11 格", GUI.skin.button)) _terrainBrushRadius = 5;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("重置地图和单位", GUILayout.Height(27))) ResetTerrain();
            GUILayout.Label("左键拖动编辑；右键移动\n切换算法保留地图；仅受阻路线重寻路");
            GUILayout.Label(TerrainBusy ? "后台更新地图，未受影响的单位继续移动" : _terrainStatus);
            GUILayout.EndArea();
            GUI.enabled = enabled;
        }

        private void GuardPendingTerrainWalls()
        {
            if (!IsRunning || _mode != NavigationMode.NavMesh || _navAppliedTerrain == null) return;
            for (int i = 0; i < _navUnits.Count; i++)
            {
                NavMeshAgent agent = _navUnits[i].Agent;
                if (!agent.isActiveAndEnabled || !agent.isOnNavMesh || !_unitOrderAccepted[i] || !agent.hasPath) continue;
                Vector3 position = agent.nextPosition;
                float horizon = Mathf.Max(0.05f, Time.deltaTime * 2);
                Vector3 ahead = Vector3.MoveTowards(position, agent.steeringTarget, Mathf.Max(0.25f, agent.speed * horizon));
                // 仅拦住即将进入尚未烘焙的新墙的单位，其余单位沿现有路径继续走。
                bool blocked = TerrainBusy && (PendingTerrainWallIntersects(position, ahead) ||
                    PendingTerrainWallIntersects(position, position + agent.velocity * horizon));
                if (blocked)
                {
                    _terrainHeldUnits[i] = true;
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                else if (_terrainHeldUnits[i])
                {
                    _terrainHeldUnits[i] = false;
                    agent.isStopped = false;
                }
            }
        }

        private bool PendingTerrainWallIntersects(Vector3 start, Vector3 end)
        {
            const float clearance = 0.8f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - Offset.x + 50 - clearance));
            int maxX = Mathf.Min(GridWidth - 1, Mathf.FloorToInt(Mathf.Max(start.x, end.x) - Offset.x + 50 + clearance));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.z, end.z) - Offset.z + 30 - clearance));
            int maxZ = Mathf.Min(GridHeight - 1, Mathf.FloorToInt(Mathf.Max(start.z, end.z) - Offset.z + 30 + clearance));
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    bool buildingWall = _navBuildingTerrain != null && _navBuildingTerrain[x, z] == TerrainKind.Wall;
                    bool stableWall = _navAppliedTerrain[x, z] == TerrainKind.Wall &&
                        (_navBuildingTerrain == null || buildingWall);
                    // 最新地图和在途快照都可能增加墙体；只有两份导航快照都已阻挡的墙才交给原网格处理。
                    if ((_terrain[x, z] != TerrainKind.Wall && !buildingWall) || stableWall) continue;
                    Vector3 center = CellCenter(x, z);
                    float enter = 0, exit = 1;
                    if (ClipAxis(start.x, end.x - start.x, center.x - 0.5f - clearance, center.x + 0.5f + clearance, ref enter, ref exit) &&
                        ClipAxis(start.z, end.z - start.z, center.z - 0.5f - clearance, center.z + 0.5f + clearance, ref enter, ref exit)) return true;
                }
            return false;
        }

        private void UpdateNavMeshTerrainSpeed()
        {
            foreach (var mover in _navUnits) mover.Agent.speed = UnitSpeed * TerrainSpeedAt(mover.transform.position);
            GuardPendingTerrainWalls();
        }

        private void UpdateTerrainNavigation()
        {
            if (!_terrainDirty || _terrainRebuilding || Time.realtimeSinceStartup < _nextTerrainCommit) return;
            _terrainRebuilding = true;
            StartCoroutine(RebuildTerrainNavigation());
        }

        private IEnumerator RebuildTerrainNavigation()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                do
                {
                    _terrainDirty = false;
                    int revision = TerrainRevision;
                    BuildTerrainRectangles();
                    RefreshTerrainVisuals();
                    bool opened = HasTerrainOpening();
                    ApplyTerrainToFlowField();
                    if (_mode == NavigationMode.NavMesh)
                    {
                        MarkAffectedNavMeshRoutes(_navAppliedTerrain, _terrain);
                        _navBuildingTerrain = (TerrainKind[,])_terrain.Clone();
                        _terrainNavUpdate = NavMeshBuilder.UpdateNavMeshDataAsync(_navData, NavMesh.GetSettingsByID(0),
                            TerrainNavigationSources(), new Bounds(Vector3.zero, new Vector3(102, 10, 62)));
                        yield return _terrainNavUpdate;
                        _terrainNavUpdate = null;
                        // 等待下一次导航系统更新，让 Agent 使用新网格后再查询路径和恢复移动。
                        yield return null;
                        // 异步期间仍可接收玩家命令，需要把新路径也纳入这批墙体的影响检查。
                        MarkAffectedNavMeshRoutes(_navAppliedTerrain, _navBuildingTerrain);
                        _navAppliedTerrain = _navBuildingTerrain;
                        _navBuildingTerrain = null;
                    }
                    NavigationRevision = revision;
                    // 地图更新和玩家下令分开；自动绕行保留原站位和短暂路线反馈的计时。
                    RefreshAffectedTerrainRoutes(opened);
                } while (_terrainDirty);
            }
            finally { _terrainRebuilding = false; }
            _terrainStatus = "地图更新 " + timer.Elapsed.TotalMilliseconds.ToString("F1") + " ms；" +
                (_mode == NavigationMode.NavMesh ? "本批重寻路 " + LastTerrainRepathCount + " / 400" :
                "本批共享流场更新 " + LastTerrainFlowRebuildCount + " 次");
        }

        private static byte TerrainCost(TerrainKind kind)
        {
            return kind == TerrainKind.Wall ? FlowField.CostWall : kind == TerrainKind.Swamp ? SwampTerrainCost :
                kind == TerrainKind.Road ? RoadTerrainCost : NormalTerrainCost;
        }

        private bool IsTerrainCellBlocked(int x, int z)
        {
            if (x < 0 || x >= GridWidth || z < 0 || z >= GridHeight) return true;
            // 命令使用最新笔刷数据判断，不能等延迟提交后的流场；墙边余量与流场保持一致。
            for (int nx = Mathf.Max(0, x - 1); nx <= Mathf.Min(GridWidth - 1, x + 1); nx++)
                for (int nz = Mathf.Max(0, z - 1); nz <= Mathf.Min(GridHeight - 1, z + 1); nz++)
                    if (_terrain[nx, nz] == TerrainKind.Wall) return true;
            return false;
        }

        private Vector3[] FindUnitSpawnPositions()
        {
            var positions = new Vector3[UnitCount];
            var assigned = new bool[UnitCount];
            int missing = 0;
            for (int i = 0; i < UnitCount; i++)
            {
                positions[i] = Offset + new Vector3(-38f + i % Columns * Spacing, 0,
                    (i / Columns - (Columns - 1) * 0.5f) * Spacing);
                assigned[i] = TryGetTerrainSpawn(positions[i], out Vector3 safe);
                if (assigned[i]) positions[i] = safe;
                else missing++;
            }
            if (missing == 0) return positions;

            var candidates = new Vector3[GridWidth, GridHeight];
            var available = new bool[GridWidth, GridHeight];
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                    available[x, z] = TryGetTerrainSpawn(CellCenter(x, z), out candidates[x, z]);
            // 先为所有仍然可用的原始站位留空，补位时不会挤占尚未创建的单位。
            for (int i = 0; i < UnitCount; i++)
                if (assigned[i]) ReserveTerrainSpawn(positions[i], candidates, available);
            for (int i = 0; i < UnitCount; i++)
            {
                if (assigned[i]) continue;
                int bestX = -1, bestZ = -1;
                float nearest = float.PositiveInfinity;
                for (int x = 0; x < GridWidth; x++)
                    for (int z = 0; z < GridHeight; z++)
                    {
                        if (!available[x, z]) continue;
                        float distance = (candidates[x, z] - positions[i]).sqrMagnitude;
                        if (distance >= nearest) continue;
                        nearest = distance;
                        bestX = x;
                        bestZ = z;
                    }
                if (bestX < 0) throw new InvalidOperationException("地图空地不足，无法安全放置 400 个单位，请先擦除部分障碍物。");
                positions[i] = candidates[bestX, bestZ];
                ReserveTerrainSpawn(positions[i], candidates, available);
            }
            return positions;
        }

        private bool TryGetTerrainSpawn(Vector3 candidate, out Vector3 position)
        {
            position = candidate;
            if (!WorldToCell(candidate, out int x, out int z) || _field.IsWall(x, z) ||
                !CanTravel(candidate - Offset, candidate - Offset) ||
                !NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.25f, NavMesh.AllAreas)) return false;
            float dx = hit.position.x - candidate.x, dz = hit.position.z - candidate.z;
            if (dx * dx + dz * dz > 0.01f * 0.01f) return false;
            position = hit.position;
            if (_mode == NavigationMode.FlowField) position.y = 0;
            return true;
        }

        private static void ReserveTerrainSpawn(Vector3 position, Vector3[,] candidates, bool[,] available)
        {
            WorldToCell(position, out int cx, out int cz);
            for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(GridWidth - 1, cx + 1); x++)
                for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(GridHeight - 1, cz + 1); z++)
                {
                    Vector3 delta = candidates[x, z] - position;
                    delta.y = 0;
                    if (delta.sqrMagnitude < UnitRadius * UnitRadius * 4) available[x, z] = false;
                }
        }

        private void ApplyTerrainToFlowField()
        {
            _hasWeightedTerrain = false;
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                {
                    _field.Cost[x, z] = TerrainCost(_terrain[x, z]);
                    if (_terrain[x, z] == TerrainKind.Swamp || _terrain[x, z] == TerrainKind.Road) _hasWeightedTerrain = true;
                }
            // 流场按单位半径给障碍物外围预留一格，实际移动仍检查真实墙体边界。
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                    if (_terrain[x, z] == TerrainKind.Wall)
                        for (int nx = Mathf.Max(0, x - 1); nx <= Mathf.Min(GridWidth - 1, x + 1); nx++)
                            for (int nz = Mathf.Max(0, z - 1); nz <= Mathf.Min(GridHeight - 1, z + 1); nz++)
                                _field.Cost[nx, nz] = FlowField.CostWall;
        }

        private void UpdateWeightedFlowVectors()
        {
            if (!_hasWeightedTerrain) return;
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                {
                    _field.Vectors[x, z] = default;
                    if (_field.IsWall(x, z) || float.IsInfinity(_field.Integration[x, z])) continue;
                    float best = float.PositiveInfinity;
                    foreach (GridDir direction in FlowField.Dirs8)
                    {
                        int nx = x + direction.Dx, nz = z + direction.Dy;
                        if (_field.IsWall(nx, nz) || _field.CutsCorner(x, z, direction) ||
                            _field.Integration[nx, nz] >= _field.Integration[x, z]) continue;
                        float cost = _field.Integration[nx, nz] + _field.Cost[x, z] * direction.DistMul;
                        if (cost >= best) continue;
                        best = cost;
                        _field.Vectors[x, z] = new FlowVector { X = direction.Dx / direction.DistMul, Y = direction.Dy / direction.DistMul };
                    }
                }
        }

        private bool IsFlowPositionReachable(Vector3 world)
        {
            if (!WorldToCell(world, out int x, out int z)) return false;
            if (!_field.IsWall(x, z)) return !float.IsInfinity(_field.Integration[x, z]);
            Vector3 nearest = FindNearestWalkable(world, true);
            return WorldToCell(nearest, out x, out z) && !_field.IsWall(x, z) && !float.IsInfinity(_field.Integration[x, z]);
        }

        private bool CanFollowStraightRoute(Vector3 start, Vector3 end)
        {
            if (!CanTravel(start, end)) return false;
            if (!_hasWeightedTerrain) return true;
            if (!WorldToCell(Offset + start, out int sx, out int sz) || !WorldToCell(Offset + end, out int tx, out int tz)) return false;
            float from = _field.Integration[sx, sz], to = _field.Integration[tx, tz];
            if (float.IsInfinity(from) || float.IsInfinity(to)) return false;
            float distance = Vector3.Distance(start, end);
            // 到达自己站位的最后一段允许展开队形，其余路段要比较地形通行成本。
            if (distance < 1.5f || from <= to + NormalTerrainCost) return true;
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance * 2));
            float cost = 0;
            for (int i = 0; i < samples; i++)
            {
                Vector3 world = Offset + Vector3.Lerp(start, end, (i + 0.5f) / samples);
                if (!WorldToCell(world, out int x, out int z)) return false;
                cost += TerrainCost(_terrain[x, z]) * distance / samples;
            }
            return cost <= from - to + NormalTerrainCost * 2;
        }

        private bool HasCentralTerrainWall(float centerZ, float extent)
        {
            foreach (TerrainRectangle rectangle in _terrainRectangles)
                if (rectangle.Kind == TerrainKind.Wall && rectangle.X <= 50 && rectangle.X + rectangle.Width >= 50 &&
                    rectangle.Z - 30 < centerZ + extent && rectangle.Z + rectangle.Depth - 30 > centerZ - extent) return true;
            return false;
        }

        private void BuildTerrainRectangles()
        {
            _terrainRectangles.Clear();
            var visited = new bool[GridWidth, GridHeight];
            for (int z = 0; z < GridHeight; z++)
                for (int x = 0; x < GridWidth; x++)
                {
                    TerrainKind kind = _terrain[x, z];
                    if (kind == TerrainKind.Normal || visited[x, z]) continue;
                    int width = 1, depth = 1;
                    while (x + width < GridWidth && !visited[x + width, z] && _terrain[x + width, z] == kind) width++;
                    bool extend = true;
                    while (z + depth < GridHeight && extend)
                    {
                        for (int xx = x; xx < x + width; xx++)
                            if (visited[xx, z + depth] || _terrain[xx, z + depth] != kind) { extend = false; break; }
                        if (extend) depth++;
                    }
                    for (int zz = z; zz < z + depth; zz++)
                        for (int xx = x; xx < x + width; xx++) visited[xx, zz] = true;
                    _terrainRectangles.Add(new TerrainRectangle { X = x, Z = z, Width = width, Depth = depth, Kind = kind });
                }
            _terrainRectanglesRevision = TerrainRevision;
        }

        private List<NavMeshBuildSource> TerrainNavigationSources()
        {
            var sources = new List<NavMeshBuildSource>(_terrainRectangles.Count + 1)
            {
                Source(new Vector3(0, -0.5f, 0), new Vector3(100, 1, 60))
            };
            foreach (TerrainRectangle rectangle in _terrainRectangles)
            {
                Vector3 center = rectangle.Center;
                center.y = 1;
                var source = Source(center, new Vector3(rectangle.Width, 2, rectangle.Depth));
                source.area = rectangle.Kind == TerrainKind.Wall ? 1 : rectangle.Kind == TerrainKind.Swamp ? SwampArea : RoadArea;
                if (rectangle.Kind != TerrainKind.Wall)
                {
                    source.shape = NavMeshBuildSourceShape.ModifierBox;
                    source.size = new Vector3(rectangle.Width, 4, rectangle.Depth);
                }
                sources.Add(source);
            }
            return sources;
        }

        private void CreateTerrainVisuals()
        {
            Mesh cube = _world.transform.Find("Ground").GetComponent<MeshFilter>().sharedMesh;
            _terrainCubeVertices = cube.vertices;
            _terrainCubeTriangles = cube.triangles;
            for (int i = 0; i < _terrainMeshes.Length; i++)
            {
                var terrainObject = new GameObject("Editable Terrain " + ((TerrainKind)(i + 1)));
                terrainObject.transform.SetParent(_world.transform, false);
                var mesh = new Mesh { name = terrainObject.name, indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
                _terrainMeshes[i] = mesh;
                terrainObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = terrainObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                Tint(renderer, i == 0 ? new Color32(169, 110, 69, 255) : i == 1 ? new Color32(113, 69, 157, 255) : new Color32(197, 179, 105, 255));
            }
            RefreshTerrainVisuals();
        }

        private void RefreshTerrainVisuals()
        {
            for (int i = 0; i < _terrainMeshes.Length; i++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                foreach (TerrainRectangle rectangle in _terrainRectangles)
                {
                    if ((int)rectangle.Kind != i + 1) continue;
                    int first = vertices.Count;
                    Vector3 center = Offset + rectangle.Center;
                    if (rectangle.Kind == TerrainKind.Wall)
                    {
                        center.y = 1;
                        Vector3 size = new Vector3(rectangle.Width, 2, rectangle.Depth);
                        foreach (Vector3 vertex in _terrainCubeVertices) vertices.Add(center + Vector3.Scale(vertex, size));
                        foreach (int triangle in _terrainCubeTriangles) triangles.Add(first + triangle);
                    }
                    else
                    {
                        center.y = 0.012f;
                        float hx = rectangle.Width * 0.5f, hz = rectangle.Depth * 0.5f;
                        vertices.Add(center + new Vector3(-hx, 0, -hz));
                        vertices.Add(center + new Vector3(-hx, 0, hz));
                        vertices.Add(center + new Vector3(hx, 0, hz));
                        vertices.Add(center + new Vector3(hx, 0, -hz));
                        triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                    }
                }
                Mesh mesh = _terrainMeshes[i];
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }

        private void ReleaseTerrainResources()
        {
            if (_terrainNavUpdate != null && _navData != null) NavMeshBuilder.Cancel(_navData);
            _terrainNavUpdate = null;
            _terrainRebuilding = false;
            _terrainDirty = false;
            _paintingTerrain = false;
            _hasPaintAnchor = false;
            _navAppliedTerrain = null;
            _navBuildingTerrain = null;
            Array.Clear(_terrainHeldUnits, 0, _terrainHeldUnits.Length);
            ResetTerrainRepathState();
            for (int i = 0; i < _terrainMeshes.Length; i++)
                if (_terrainMeshes[i] != null)
                {
                    Destroy(_terrainMeshes[i]);
                    _terrainMeshes[i] = null;
                }
        }
    }
}
