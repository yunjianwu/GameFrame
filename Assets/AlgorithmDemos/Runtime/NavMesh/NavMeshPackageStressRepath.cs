using System;
using Starter.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AlgorithmDemos.NavMeshExample
{
    public sealed partial class NavMeshPackageStress
    {
        private readonly bool[] _terrainRepathPending = new bool[UnitCount];
        private bool _queuedTerrainOrder;
        private bool _flowFieldDirty;

        public int LastTerrainRepathCount { get; private set; }
        public int LastTerrainFlowRebuildCount { get; private set; }

        private bool HasTerrainOpening()
        {
            for (int x = 0; x < GridWidth; x++)
                for (int z = 0; z < GridHeight; z++)
                    if (_field.IsWall(x, z) && !IsTerrainCellBlocked(x, z)) return true;
            return false;
        }

        private void MarkAffectedNavMeshRoutes(TerrainKind[,] previousTerrain, TerrainKind[,] nextTerrain)
        {
            for (int i = 0; i < _navUnits.Count; i++)
            {
                if (!_unitOrderAccepted[i]) continue;
                NavMeshAgent agent = _navUnits[i].Agent;
                if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                if (_terrainHeldUnits[i] || TargetBlockedByTerrain(i))
                {
                    _terrainRepathPending[i] = true;
                    continue;
                }
                if (!agent.hasPath) continue;
                int corners = agent.path.GetCornersNonAlloc(_navCorners);
                Vector3 previous = agent.nextPosition - Offset;
                for (int corner = 0; corner < corners; corner++)
                {
                    Vector3 next = _navCorners[corner] - Offset;
                    if (AddedTerrainWallIntersects(previous + Offset, next + Offset, previousTerrain, nextTerrain))
                    {
                        _terrainRepathPending[i] = true;
                        break;
                    }
                    previous = next;
                }
            }
        }

        private bool AddedTerrainWallIntersects(Vector3 start, Vector3 end, TerrainKind[,] previous, TerrainKind[,] next)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - Offset.x + 50 - UnitRadius));
            int maxX = Mathf.Min(GridWidth - 1, Mathf.FloorToInt(Mathf.Max(start.x, end.x) - Offset.x + 50 + UnitRadius));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.z, end.z) - Offset.z + 30 - UnitRadius));
            int maxZ = Mathf.Min(GridHeight - 1, Mathf.FloorToInt(Mathf.Max(start.z, end.z) - Offset.z + 30 + UnitRadius));
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (next[x, z] != TerrainKind.Wall || previous[x, z] == TerrainKind.Wall) continue;
                    Vector3 center = CellCenter(x, z);
                    float enter = 0, exit = 1;
                    if (ClipAxis(start.x, end.x - start.x, center.x - 0.5f - UnitRadius, center.x + 0.5f + UnitRadius, ref enter, ref exit) &&
                        ClipAxis(start.z, end.z - start.z, center.z - 0.5f - UnitRadius, center.z + 0.5f + UnitRadius, ref enter, ref exit)) return true;
                }
            return false;
        }

        private bool TargetBlockedByTerrain(int index)
        {
            return !WorldToCell(_targets[index], out int x, out int z) || IsTerrainCellBlocked(x, z);
        }

        private void RefreshAffectedTerrainRoutes(bool opened)
        {
            LastTerrainRepathCount = 0;
            LastTerrainFlowRebuildCount = 0;
            if (_queuedTerrainOrder)
            {
                AssignMoveOrder(Destination, false);
                return;
            }

            if (_mode == NavigationMode.FlowField)
            {
                _flowFieldDirty = true;
                if (FlowRoutesAffected(opened)) RefreshFlowTerrainRoutes();
            }
            else
            {
                for (int i = 0; i < _navUnits.Count; i++)
                {
                    NavMeshAgent agent = _navUnits[i].Agent;
                    if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                    bool away = (_unitTransforms[i].position - _targets[i]).sqrMagnitude > 0.4f * 0.4f;
                    bool invalid = _unitOrderAccepted[i] && away &&
                        (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete);
                    if (!_terrainRepathPending[i] && !_terrainHeldUnits[i] && !invalid &&
                        !(!_unitOrderAccepted[i] && opened)) continue;
                    _terrainRepathPending[i] = false;
                    _terrainHeldUnits[i] = false;
                    LastTerrainRepathCount++;
                    _unitOrderAccepted[i] = !TargetBlockedByTerrain(i) &&
                        _navUnits[i].MoveTo(_targets[i]) == MoveRequestResult.Accepted;
                    if (!_unitOrderAccepted[i]) _navUnits[i].Stop();
                }
                GuardPendingTerrainWalls();
                RecountAcceptedTerrainOrders();
            }
            _nextRouteRefresh = 0;
            _nextRefresh = 0;
        }

        private bool FlowRoutesAffected(bool opened)
        {
            for (int i = 0; i < _unitTransforms.Count; i++)
            {
                if (!_unitOrderAccepted[i])
                {
                    if (opened) return true;
                    continue;
                }
                if (TargetBlockedByTerrain(i)) return true;
                if ((_unitTransforms[i].position - _targets[i]).sqrMagnitude <= 0.2f * 0.2f) continue;
                // 沿当前共享场检查已有路线；无关区域的编辑只更新地图成本，不强迫整队改道。
                if (!TraceFlowRoute(i, out _)) return true;
            }
            return false;
        }

        private void RefreshFlowTerrainRoutes()
        {
            _field.SetTarget(_field.TargetX, _field.TargetY);
            UpdateWeightedFlowVectors();
            _flowFieldDirty = false;
            LastTerrainFlowRebuildCount++;
            for (int i = 0; i < _unitTransforms.Count; i++)
                _unitOrderAccepted[i] = !TargetBlockedByTerrain(i) &&
                    IsFlowPositionReachable(_unitTransforms[i].position) && IsFlowPositionReachable(_targets[i]);
            RecountAcceptedTerrainOrders();
            _nextRouteRefresh = 0;
            _nextRefresh = 0;
        }

        private void RecountAcceptedTerrainOrders()
        {
            AcceptedCount = 0;
            for (int i = 0; i < _unitTransforms.Count; i++)
                if (_unitOrderAccepted[i]) AcceptedCount++;
            _status = AcceptedCount == UnitCount ? "继续执行原移动命令" : "部分路线受阻，等待通路恢复";
        }

        private void ResetTerrainRepathState()
        {
            Array.Clear(_terrainRepathPending, 0, _terrainRepathPending.Length);
            _queuedTerrainOrder = false;
            _flowFieldDirty = false;
            LastTerrainRepathCount = 0;
            LastTerrainFlowRebuildCount = 0;
        }
    }
}
