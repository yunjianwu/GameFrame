using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AlgorithmDemos;
using Demos.FlowFieldPathfinding;
using Starter.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace AlgorithmDemos.NavMeshExample
{
    public sealed partial class NavMeshPackageStress : MonoBehaviour
    {
        private enum NavigationMode { NavMesh, FlowField }
        private enum DisplayMode { Classic, Formation, Group, All }
        private enum DisplayPolicy { Auto, Always, Hidden }

        private const int Columns = 20;
        private const int UnitCount = Columns * Columns;
        private const int GridWidth = 100;
        private const int GridHeight = 60;
        private const int BucketWidth = 50;
        private const int BucketHeight = 30;
        private const float Spacing = 1.15f;
        private const float UnitRadius = 0.4f;
        private const float UnitSpeed = 4f;
        private const float RouteHeight = 0.035f;
        private const float RouteWidth = 0.08f;
        private const float PreviewHoldSeconds = 0.6f;
        private const float PreviewFadeSeconds = 0.4f;
        private static readonly Vector3 Offset = new Vector3(200, 0, 0);
        private static readonly Rect Panel = new Rect(12, 76, 400, 270);
        private static readonly int[] SampleRows = { 0, 3, 6, 9, 12, 15, 19 };
        private static readonly int[] SampleColumns = { 0, 4, 9, 14, 19 };
        private static readonly (Vector3 center, Vector3 size)[] Walls =
        {
            (new Vector3(0, 1, -24.5f), new Vector3(2, 2, 11)),
            (new Vector3(0, 1, -8f), new Vector3(2, 2, 10)),
            (new Vector3(0, 1, 8f), new Vector3(2, 2, 10)),
            (new Vector3(0, 1, 24.5f), new Vector3(2, 2, 11))
        };

        private readonly List<NavMeshUnitMover> _navUnits = new List<NavMeshUnitMover>(UnitCount);
        private readonly List<Transform> _unitTransforms = new List<Transform>(UnitCount);
        private readonly Vector3[] _targets = new Vector3[UnitCount];
        private readonly Vector3[] _pendingTargets = new Vector3[UnitCount];
        private readonly Vector3[] _positions = new Vector3[UnitCount];
        private readonly int[] _bucketHeads = new int[BucketWidth * BucketHeight];
        private readonly int[] _bucketNext = new int[UnitCount];
        private readonly Vector3[] _routePoints = new Vector3[256];
        private readonly Vector3[] _navCorners = new Vector3[128];
        private readonly List<Vector3> _routeVertices = new List<Vector3>(16000);
        private readonly List<Color32> _routeColors = new List<Color32>(16000);
        private readonly List<int> _routeTriangles = new List<int>(24000);
        private readonly int[] _representativeIndices = new int[3];
        private readonly int[] _representativeBranches = new int[3];
        private NavigationMode _mode;
        private DisplayMode _displayMode = DisplayMode.All;
        private DisplayPolicy _displayPolicy = DisplayPolicy.Auto;
        private DisplayPolicy _lastVisiblePolicy = DisplayPolicy.Auto;
        private NavMeshDemo _normalDemo;
        private Camera _normalCamera;
        private Camera _stressCamera;
        private GameObject _world;
        private NavMeshData _navData;
        private NavMeshDataInstance _navInstance;
        private FlowField _field;
        private Material _sourceMaterial;
        private Material _routeMaterial;
        private Mesh _routeMesh;
        private MeshRenderer _routeRenderer;
        private Transform _routeTransform;
        private bool _preparing;
        private float _started;
        private float _nextRefresh;
        private float _nextRouteRefresh;
        private float _previewStarted;
        private float _previewOpacity;
        private float _fps;
        private int _rightOfWall;
        private int _arrived;
        private double _orderMs;
        private string _status;
        private Vector3 _commandDirection = Vector3.right;

        public bool IsRunning { get; private set; }
        public int AcceptedCount { get; private set; }
        public int OrderSequence { get; private set; }
        public string ModeName => _mode.ToString();
        public Vector3 Destination { get; private set; }
        public bool RoutesEnabled => _displayPolicy != DisplayPolicy.Hidden;
        public string DisplayModeName => _displayMode.ToString();
        public string DisplayPolicyName => _displayPolicy.ToString();
        public float PreviewStartedAt => _previewStarted;
        public bool OverlayVisible => _routeRenderer != null && _routeRenderer.enabled;
        public int RenderedRouteCount { get; private set; }
        public int CompleteRouteCount { get; private set; }

        private void Awake()
        {
            NavMesh.onPreUpdate += GuardPendingTerrainWalls;
            _normalDemo = GetComponent<NavMeshDemo>();
            _normalCamera = GetComponentInChildren<Camera>();
            Transform ground = transform.Find("Ground");
            if (ground != null) _sourceMaterial = ground.GetComponent<Renderer>().sharedMaterial;
        }

        private void Update()
        {
            if (!IsRunning) return;
            HandleTerrainInput();
            UpdateTerrainNavigation();
            if (Input.GetMouseButtonDown(1)) TryIssueScreenOrder(Input.mousePosition);
            if (_mode == NavigationMode.FlowField) MoveFlowUnits();
            else UpdateNavMeshTerrainSpeed();
            UpdatePreviewVisibility();
            if (OverlayVisible && Time.realtimeSinceStartup >= _nextRouteRefresh)
            {
                RefreshRoutes();
                _nextRouteRefresh = Time.realtimeSinceStartup + 0.5f;
            }
            if (Time.unscaledDeltaTime > 0)
                _fps = Mathf.Lerp(_fps, 1f / Time.unscaledDeltaTime, 0.05f);
            if (Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 0.5f;
            _rightOfWall = 0;
            _arrived = 0;
            for (int i = 0; i < _unitTransforms.Count; i++)
            {
                Vector3 position = _unitTransforms[i].position;
                if (position.x > Offset.x + 2f) _rightOfWall++;
                if ((position - _targets[i]).sqrMagnitude <= 0.8f * 0.8f) _arrived++;
            }
        }

        private void OnGUI()
        {
            DemoPresentation.BeginGui();
            if (_world == null && !_preparing && _status == null)
            {
                if (GUI.Button(new Rect(12, 220, 340, 34), "开始 400 单位移动测试")) BeginStress();
                return;
            }

            string modeText = _mode == NavigationMode.NavMesh
                ? "NavMesh：逐单位路径 + Unity 避让"
                : "流场：共享方向场 + 简易分离";
            string detail = _preparing ? "正在创建测试地图和单位..." :
                IsRunning ? modeText + "\n" +
                    "接收指令：" + AcceptedCount + " / 400    发令耗时：" + _orderMs.ToString("F2") + " ms\n" +
                    "墙体右侧：" + _rightOfWall + " / 400    接近各自站位：" + _arrived + " / 400\n" +
                    "移动：" + (Time.realtimeSinceStartup - _started).ToString("F1") + " 秒    帧率：" + _fps.ToString("F0") + " FPS\n" +
                    "右键移动：" + _status : _status;
            GUI.Box(new Rect(12, 76, 400, 132), "400 单位寻路对照  /  " + DisplayModeLabel() + "\n" + detail);
            bool previous = GUI.enabled;
            GUI.enabled = !_preparing && _mode != NavigationMode.NavMesh;
            if (GUI.Button(new Rect(12, 214, 128, 32), "切换 NavMesh")) SwitchToNavMesh();
            GUI.enabled = !_preparing && _mode != NavigationMode.FlowField;
            if (GUI.Button(new Rect(148, 214, 128, 32), "切换流场")) SwitchToFlowField();
            GUI.enabled = !_preparing;
            if (GUI.Button(new Rect(284, 214, 128, 32), "重新开始")) BeginStress();
            if (GUI.Button(new Rect(12, 252, 170, 32), "返回普通演示")) ReturnToDemo();
            if (GUI.Button(new Rect(190, 252, 222, 32), "显示：" + DisplayPolicyLabel())) CycleDisplayPolicy();

            GUI.enabled = !_preparing && _displayMode != DisplayMode.Classic;
            if (GUI.Button(new Rect(12, 290, 96, 32), "目标标记")) SetDisplayMode("Classic");
            GUI.enabled = !_preparing && _displayMode != DisplayMode.Formation;
            if (GUI.Button(new Rect(112, 290, 96, 32), "编队预览")) SetDisplayMode("Formation");
            GUI.enabled = !_preparing && _displayMode != DisplayMode.Group;
            if (GUI.Button(new Rect(212, 290, 96, 32), "代表路线")) SetDisplayMode("Group");
            GUI.enabled = !_preparing && _displayMode != DisplayMode.All;
            if (GUI.Button(new Rect(312, 290, 100, 32), "全部路线")) SetDisplayMode("All");
            GUI.enabled = previous;
            GUI.Label(new Rect(16, 326, 395, 20), "短暂模式：右键后约 1 秒淡出；按住 Alt 查看");
            GUI.enabled = previous;
            DrawTerrainControls();
        }

        public void ToggleRoutes()
        {
            SetDisplayPolicy((_displayPolicy == DisplayPolicy.Hidden ? _lastVisiblePolicy : DisplayPolicy.Hidden).ToString());
        }

        public void SetDisplayMode(string name)
        {
            if (!Enum.TryParse(name, out DisplayMode mode) || !Enum.IsDefined(typeof(DisplayMode), mode)) return;
            _displayMode = mode;
            _previewStarted = Time.realtimeSinceStartup;
            _nextRouteRefresh = 0;
            RenderedRouteCount = 0;
            CompleteRouteCount = 0;
            UpdatePreviewVisibility();
        }

        public void SetDisplayPolicy(string name)
        {
            if (!Enum.TryParse(name, out DisplayPolicy policy) || !Enum.IsDefined(typeof(DisplayPolicy), policy)) return;
            _displayPolicy = policy;
            if (policy != DisplayPolicy.Hidden) _lastVisiblePolicy = policy;
            if (policy == DisplayPolicy.Auto) _previewStarted = Time.realtimeSinceStartup;
            _nextRouteRefresh = 0;
            UpdatePreviewVisibility();
        }

        private void CycleDisplayPolicy()
        {
            if (_displayPolicy == DisplayPolicy.Auto) SetDisplayPolicy("Always");
            else if (_displayPolicy == DisplayPolicy.Always) SetDisplayPolicy("Hidden");
            else SetDisplayPolicy("Auto");
        }

        public float PreviewOpacityAt(float now, bool altHeld)
        {
            if (_displayPolicy == DisplayPolicy.Hidden || OrderSequence == 0) return 0;
            if (_displayPolicy == DisplayPolicy.Always || altHeld) return 1;
            float elapsed = Mathf.Max(0, now - _previewStarted);
            return Mathf.Clamp01((PreviewHoldSeconds + PreviewFadeSeconds - elapsed) / PreviewFadeSeconds);
        }

        private void UpdatePreviewVisibility()
        {
            if (_routeRenderer == null || _routeMaterial == null) return;
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            _previewOpacity = PreviewOpacityAt(Time.realtimeSinceStartup, altHeld);
            bool wasVisible = _routeRenderer.enabled;
            _routeRenderer.enabled = _previewOpacity > 0;
            _routeMaterial.color = new Color(1, 1, 1, _previewOpacity);
            if (_routeRenderer.enabled && !wasVisible) _nextRouteRefresh = 0;
            if (!_routeRenderer.enabled)
            {
                RenderedRouteCount = 0;
                CompleteRouteCount = 0;
            }
        }

        private string DisplayModeLabel()
        {
            switch (_displayMode)
            {
                case DisplayMode.Classic: return "目标标记";
                case DisplayMode.Formation: return "编队预览";
                case DisplayMode.Group: return "代表路线";
                default: return "全部路线";
            }
        }

        private string DisplayPolicyLabel()
        {
            switch (_displayPolicy)
            {
                case DisplayPolicy.Auto: return "短暂";
                case DisplayPolicy.Always: return "常显";
                default: return "隐藏";
            }
        }

        public void SwitchToNavMesh()
        {
            if (_preparing || _mode == NavigationMode.NavMesh) return;
            _mode = NavigationMode.NavMesh;
            BeginStress();
        }

        public void SwitchToFlowField()
        {
            if (_preparing || _mode == NavigationMode.FlowField) return;
            _mode = NavigationMode.FlowField;
            BeginStress();
        }

        public void BeginStress()
        {
            if (_preparing) return;
            StopAllCoroutines();
            ReleaseWorld();
            IsRunning = false;
            AcceptedCount = 0;
            OrderSequence = 0;
            _rightOfWall = 0;
            _arrived = 0;
            _fps = 0;
            _nextRouteRefresh = 0;
            _status = null;
            _preparing = true;
            if (_normalDemo != null) _normalDemo.enabled = false;
            if (_normalCamera != null) _normalCamera.enabled = false;
            StartCoroutine(PrepareAndMove());
        }

        public void ReturnToDemo()
        {
            StopAllCoroutines();
            _preparing = false;
            IsRunning = false;
            _status = null;
            ReleaseWorld();
            if (_normalCamera != null) _normalCamera.enabled = true;
            if (_normalDemo != null) _normalDemo.enabled = true;
        }

        private IEnumerator PrepareAndMove()
        {
            yield return null;
            bool prepared = true;
            try
            {
                EnsureTerrainMap();
                CreateWorld();
                CreateNavigation();
                CreateFlowField();
                CreateUnits();
            }
            catch (Exception error)
            {
                _preparing = false;
                _status = "无法启动测试：" + error.Message;
                Debug.LogException(error);
                prepared = false;
            }
            if (!prepared) yield break;

            // 等待导航系统注册单位，保证两种模式从同一批位置开始。
            for (int i = 0; i < 5; i++) yield return null;
            _preparing = false;
            IsRunning = true;
            IssueMoveOrder(Offset + new Vector3(26.925f, 0, 0));
            Debug.Log("NAVMESH_PACKAGE_STRESS_STARTED: " + ModeName + " " + AcceptedCount + "/" + UnitCount +
                "，指令耗时 " + _orderMs.ToString("F2") + " ms");
        }

        public bool TryIssueScreenOrder(Vector2 screenPosition)
        {
            if (!IsRunning || _stressCamera == null || screenPosition.x < 0 || screenPosition.x >= Screen.width ||
                screenPosition.y < 0 || screenPosition.y >= Screen.height || DemoInputGate.IsBlocked(screenPosition) ||
                IsTerrainPointerBlocked(screenPosition)) return false;
            Ray ray = _stressCamera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            return ground.Raycast(ray, out float distance) && IssueMoveOrder(ray.GetPoint(distance));
        }

        public bool IssueMoveOrder(Vector3 destination) => AssignMoveOrder(destination, true);

        private bool AssignMoveOrder(Vector3 destination, bool newCommand)
        {
            if (_field == null || !IsFinite(destination.x) || !IsFinite(destination.z)) return false;
            destination.y = 0;
            if (!WorldToCell(destination, out int targetX, out int targetZ) || IsTerrainCellBlocked(targetX, targetZ))
            {
                _status = "目标靠近墙体或超出地图，请重新选择";
                return false;
            }

            if (newCommand)
            {
                Destination = destination;
                OrderSequence++;
                _started = Time.realtimeSinceStartup;
            }
            if (_field.IsWall(targetX, targetZ))
            {
                _queuedTerrainOrder = true;
                _status = "已接收刚擦开的目标，通路更新后切换路线";
                return true;
            }

            _queuedTerrainOrder = false;
            var timer = Stopwatch.StartNew();
            if (_mode == NavigationMode.FlowField)
            {
                _field.SetTarget(targetX, targetZ);
                UpdateWeightedFlowVectors();
                _flowFieldDirty = false;
            }
            Vector3 formationCenter = FormationCenter(destination);
            Vector3 groupCenter = Vector3.zero;
            foreach (Transform unit in _unitTransforms) groupCenter += unit.position;
            if (_unitTransforms.Count > 0) groupCenter /= _unitTransforms.Count;
            Vector3 commandDirection = formationCenter - groupCenter;
            commandDirection.y = 0;
            if (commandDirection.sqrMagnitude > 0.0001f) _commandDirection = commandDirection.normalized;
            for (int i = 0; i < UnitCount; i++)
            {
                float x = (i % Columns - (Columns - 1) * 0.5f) * Spacing;
                float z = (i / Columns - (Columns - 1) * 0.5f) * Spacing;
                _pendingTargets[i] = ResolveSlot(formationCenter + new Vector3(x, 0, z));
            }
            AcceptedCount = 0;
            if (_mode == NavigationMode.FlowField)
            {
                // 同一目标只生成一次积分场和方向场，所有单位共享。
                for (int i = 0; i < _unitTransforms.Count; i++)
                {
                    _targets[i] = _pendingTargets[i];
                    _unitOrderAccepted[i] = IsFlowPositionReachable(_unitTransforms[i].position) &&
                        IsFlowPositionReachable(_targets[i]);
                    if (_unitOrderAccepted[i]) AcceptedCount++;
                }
            }
            else
            {
                for (int i = 0; i < _navUnits.Count; i++)
                {
                    _targets[i] = _pendingTargets[i];
                    _terrainRepathPending[i] = false;
                    _terrainHeldUnits[i] = false;
                    _unitOrderAccepted[i] = _navUnits[i].MoveTo(_targets[i]) == MoveRequestResult.Accepted;
                    if (!_unitOrderAccepted[i])
                    {
                        _navUnits[i].Stop();
                        continue;
                    }
                    AcceptedCount++;
                }
            }
            timer.Stop();
            _nextRouteRefresh = 0;
            _orderMs = timer.Elapsed.TotalMilliseconds;
            _previewStarted = Time.realtimeSinceStartup;
            _nextRefresh = _previewStarted;
            _status = AcceptedCount != UnitCount ? "部分目标不可达，开通后自动重试" :
                (formationCenter - destination).sqrMagnitude > 0.01f ? "靠近墙体或边界，队形已自动偏移" : "目标已更新";
            UpdatePreviewVisibility();
            return true;
        }

        private void CreateWorld()
        {
            if (_normalDemo == null || _normalCamera == null || _sourceMaterial == null)
                throw new InvalidOperationException("当前 NavMesh 演示缺少相机或地图材质。");
            _world = new GameObject("NavMesh 400 Stress World");
            _world.transform.SetParent(transform, false);

            var cameraObject = new GameObject("Stress Camera");
            cameraObject.transform.SetParent(_world.transform, false);
            cameraObject.transform.position = Offset + new Vector3(0, 78, -56);
            cameraObject.transform.rotation = Quaternion.LookRotation(Offset - cameraObject.transform.position);
            _stressCamera = cameraObject.AddComponent<Camera>();
            _stressCamera.orthographic = true;
            _stressCamera.orthographicSize = 40;
            _stressCamera.depth = _normalCamera.depth;
            _stressCamera.clearFlags = CameraClearFlags.SolidColor;
            _stressCamera.backgroundColor = new Color32(24, 49, 42, 255);
            _stressCamera.farClipPlane = 200;

            VisualBox("Ground", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 60), new Color32(66, 100, 82, 255));
            CreateTerrainVisuals();
            CreateRouteRenderer();
        }

        private void CreateRouteRenderer()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("无法创建路线显示材质。");
            var routeObject = new GameObject("Movement Routes");
            routeObject.transform.SetParent(_world.transform, false);
            _routeTransform = routeObject.transform;
            _routeMesh = new Mesh { name = "400 Unit Routes", indexFormat = IndexFormat.UInt32 };
            _routeMesh.MarkDynamic();
            routeObject.AddComponent<MeshFilter>().sharedMesh = _routeMesh;
            _routeRenderer = routeObject.AddComponent<MeshRenderer>();
            _routeMaterial = new Material(shader);
            _routeRenderer.sharedMaterial = _routeMaterial;
            _routeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _routeRenderer.receiveShadows = false;
            _routeRenderer.enabled = false;
        }

        private void CreateNavigation()
        {
            List<NavMeshBuildSource> sources = TerrainNavigationSources();
            _navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(102, 10, 62)), Vector3.zero, Quaternion.identity);
            if (_navData == null) throw new InvalidOperationException("无法创建测试导航网格。");
            _navInstance = NavMesh.AddNavMeshData(_navData, Offset, Quaternion.identity);
            if (!_navInstance.valid) throw new InvalidOperationException("无法加载测试导航网格。");
            _navAppliedTerrain = (TerrainKind[,])_terrain.Clone();
        }

        private void CreateFlowField()
        {
            _field = new FlowField(GridWidth, GridHeight);
            ApplyTerrainToFlowField();
            NavigationRevision = TerrainRevision;
        }

        private void CreateUnits()
        {
            Vector3[] spawnPositions = FindUnitSpawnPositions();
            for (int i = 0; i < UnitCount; i++)
            {
                var unit = new GameObject("Unit " + (i + 1));
                unit.transform.SetParent(_world.transform, false);
                unit.transform.position = spawnPositions[i];
                _unitTransforms.Add(unit.transform);
                if (_mode == NavigationMode.NavMesh)
                {
                    NavMeshUnitMover mover = unit.AddComponent<NavMeshUnitMover>();
                    mover.Agent.radius = UnitRadius;
                    mover.Agent.height = 1.8f;
                    mover.Agent.speed = UnitSpeed;
                    mover.Agent.acceleration = 12f;
                    mover.Agent.angularSpeed = 360f;
                    mover.Agent.stoppingDistance = 0.15f;
                    // 地形变化由测试层按受影响路线处理，避免旧路径标记触发隐式全队重算。
                    mover.Agent.autoRepath = false;
                    mover.Agent.avoidancePriority = 45 + i % 3;
                    mover.Agent.SetAreaCost(0, NormalTerrainCost);
                    mover.Agent.SetAreaCost(SwampArea, SwampTerrainCost);
                    mover.Agent.SetAreaCost(RoadArea, RoadTerrainCost);
                    _navUnits.Add(mover);
                }
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.transform.SetParent(unit.transform, false);
                visual.transform.localPosition = new Vector3(0, 0.9f, 0);
                visual.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                Tint(visual.GetComponent<Renderer>(), new Color32(91, 165, 213, 255));
            }
        }

        private void RefreshRoutes()
        {
            if (_routeMesh == null || _routeTransform == null) return;
            _routeVertices.Clear();
            _routeColors.Clear();
            _routeTriangles.Clear();
            RenderedRouteCount = 0;
            CompleteRouteCount = 0;
            if (_displayMode == DisplayMode.All)
            {
                for (int i = 0; i < _unitTransforms.Count; i++) AddUnitRoute(i, false);
            }
            else if (_displayMode == DisplayMode.Group) AddRepresentativeRoutes();
            else if (_displayMode == DisplayMode.Formation) AddFormationPreview();
            AddDestinationMarker();
            _routeMesh.Clear();
            _routeMesh.SetVertices(_routeVertices);
            _routeMesh.SetColors(_routeColors);
            _routeMesh.SetTriangles(_routeTriangles, 0);
            _routeMesh.RecalculateBounds();
        }

        private bool TraceUnitRoute(int index, out int count)
        {
            if (!_unitOrderAccepted[index]) { count = 0; return false; }
            return _mode == NavigationMode.NavMesh
                ? TraceNavMeshRoute(index, out count)
                : TraceFlowRoute(index, out count);
        }

        private void AddUnitRoute(int index, bool representative)
        {
            bool complete = TraceUnitRoute(index, out int pointCount);
            if (pointCount < 2) return;
            int segments = 0;
            float width = representative ? 0.24f : RouteWidth;
            Color32 color = representative ? new Color32(255, 224, 90, 210) : new Color32(255, 222, 71, 105);
            for (int point = 1; point < pointCount; point++)
                if (AddRouteSegment(_routePoints[point - 1], _routePoints[point], width, color)) segments++;
            if (segments == 0) return;
            RenderedRouteCount++;
            if (complete) CompleteRouteCount++;
        }

        private void AddRepresentativeRoutes()
        {
            int count = 0;
            int fallback = -1;
            // 从队形不同位置抽样，以测试地图的墙体通道区分代表路径。
            foreach (int row in SampleRows)
            {
                foreach (int column in SampleColumns)
                {
                    int index = row * Columns + column;
                    if (index >= _unitTransforms.Count || !TraceUnitRoute(index, out int pointCount) || pointCount < 2) continue;
                    int branch = RouteBranch(pointCount);
                    if (branch < 0)
                    {
                        if (fallback < 0) fallback = index;
                        continue;
                    }
                    bool exists = false;
                    for (int i = 0; i < count; i++)
                        if (_representativeBranches[i] == branch) exists = true;
                    if (exists) continue;
                    _representativeIndices[count] = index;
                    _representativeBranches[count++] = branch;
                    if (count == _representativeIndices.Length) break;
                }
                if (count == _representativeIndices.Length) break;
            }
            if (count == 0 && fallback >= 0) _representativeIndices[count++] = fallback;
            for (int i = 0; i < count; i++) AddUnitRoute(_representativeIndices[i], true);
        }

        private int RouteBranch(int pointCount)
        {
            float wallX = Offset.x + Walls[0].center.x;
            for (int i = 1; i < pointCount; i++)
            {
                Vector3 start = _routePoints[i - 1];
                Vector3 end = _routePoints[i];
                float delta = end.x - start.x;
                if (Mathf.Abs(delta) < 0.0001f) continue;
                float progress = (wallX - start.x) / delta;
                if (progress < 0 || progress > 1) continue;
                float localZ = Mathf.Lerp(start.z, end.z, progress) - Offset.z;
                int branch = 0;
                foreach (var wall in Walls)
                    if (wall.center.z < localZ) branch++;
                return branch;
            }
            return -1;
        }

        private void AddDestinationMarker()
        {
            const int sides = 20;
            const float radius = 1.15f;
            var color = new Color32(255, 230, 94, 235);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 start = Destination + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                Vector3 end = Destination + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                AddRouteSegment(start, end, 0.22f, color);
            }
        }

        private void AddFormationPreview()
        {
            if (AcceptedCount == 0) return;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            for (int i = 0; i < _targets.Length; i++)
            {
                minX = Mathf.Min(minX, _targets[i].x);
                maxX = Mathf.Max(maxX, _targets[i].x);
                minZ = Mathf.Min(minZ, _targets[i].z);
                maxZ = Mathf.Max(maxZ, _targets[i].z);
            }
            const float margin = 0.55f;
            var corners = new[]
            {
                new Vector3(minX - margin, 0, minZ - margin),
                new Vector3(minX - margin, 0, maxZ + margin),
                new Vector3(maxX + margin, 0, maxZ + margin),
                new Vector3(maxX + margin, 0, minZ - margin)
            };
            var color = new Color32(116, 226, 255, 205);
            for (int i = 0; i < corners.Length; i++)
                AddRouteSegment(corners[i], corners[(i + 1) % corners.Length], 0.20f, color);

            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);
            Vector3 forward = _commandDirection;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 tip = center + forward * 3f;
            AddRouteSegment(center - forward * 3f, tip, 0.24f, color);
            AddRouteSegment(tip, tip - forward * 1.5f + side, 0.24f, color);
            AddRouteSegment(tip, tip - forward * 1.5f - side, 0.24f, color);
        }

        private bool TraceNavMeshRoute(int index, out int count)
        {
            count = 0;
            NavMeshAgent agent = _navUnits[index].Agent;
            if (!agent.isOnNavMesh || !agent.hasPath) return false;
            AppendRoutePoint(ref count, _unitTransforms[index].position);
            int corners = agent.path.GetCornersNonAlloc(_navCorners);
            for (int i = 0; i < corners; i++) AppendRoutePoint(ref count, _navCorners[i]);
            return corners > 0 && corners < _navCorners.Length && agent.pathStatus == NavMeshPathStatus.PathComplete;
        }

        private bool TraceFlowRoute(int index, out int count)
        {
            count = 0;
            Vector3 position = _unitTransforms[index].position;
            Vector3 target = _targets[index];
            if ((position - target).sqrMagnitude < 0.2f * 0.2f) return false;
            AppendRoutePoint(ref count, position);
            // 从当前位置沿共享方向场逐格追踪；局部分离会让实际运动略有偏差。
            for (int step = 0; step < GridWidth + GridHeight + 32; step++)
            {
                if (CanFollowStraightRoute(position - Offset, target - Offset))
                {
                    AppendRoutePoint(ref count, target);
                    return true;
                }
                Vector3 direction = GetFlowDirection(position - Offset);
                if (direction.sqrMagnitude < 0.0001f) return false;
                Vector3 next = position + direction;
                if (!CanTravel(position - Offset, next - Offset))
                {
                    Vector3 alongX = new Vector3(next.x, position.y, position.z);
                    Vector3 alongZ = new Vector3(position.x, position.y, next.z);
                    if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z) && CanTravel(position - Offset, alongX - Offset)) next = alongX;
                    else if (CanTravel(position - Offset, alongZ - Offset)) next = alongZ;
                    else if (CanTravel(position - Offset, alongX - Offset)) next = alongX;
                    else return false;
                }
                if ((next - position).sqrMagnitude < 0.0001f) return false;
                AppendRoutePoint(ref count, next);
                position = next;
            }
            return false;
        }

        private void AppendRoutePoint(ref int count, Vector3 point)
        {
            point.y = RouteHeight;
            if (count > 0 && (point - _routePoints[count - 1]).sqrMagnitude < 0.0001f) return;
            if (count >= 2)
            {
                Vector3 previous = _routePoints[count - 1] - _routePoints[count - 2];
                Vector3 next = point - _routePoints[count - 1];
                float cross = previous.x * next.z - previous.z * next.x;
                if (Mathf.Abs(cross) < 0.0001f && Vector3.Dot(previous, next) > 0)
                {
                    _routePoints[count - 1] = point;
                    return;
                }
            }
            if (count < _routePoints.Length) _routePoints[count++] = point;
        }

        private bool AddRouteSegment(Vector3 start, Vector3 end, float width, Color32 color)
        {
            start.y = RouteHeight;
            end.y = RouteHeight;
            Vector3 direction = end - start;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) return false;
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * (width * 0.5f);
            int vertex = _routeVertices.Count;
            _routeVertices.Add(_routeTransform.InverseTransformPoint(start - side));
            _routeVertices.Add(_routeTransform.InverseTransformPoint(start + side));
            _routeVertices.Add(_routeTransform.InverseTransformPoint(end - side));
            _routeVertices.Add(_routeTransform.InverseTransformPoint(end + side));
            for (int i = 0; i < 4; i++) _routeColors.Add(color);
            _routeTriangles.Add(vertex);
            _routeTriangles.Add(vertex + 1);
            _routeTriangles.Add(vertex + 2);
            _routeTriangles.Add(vertex + 2);
            _routeTriangles.Add(vertex + 1);
            _routeTriangles.Add(vertex + 3);
            return true;
        }

        private void MoveFlowUnits()
        {
            float baseStep = UnitSpeed * Mathf.Min(Time.deltaTime, 0.05f);
            if (baseStep <= 0) return;
            for (int i = 0; i < _bucketHeads.Length; i++) _bucketHeads[i] = -1;
            for (int i = 0; i < _unitTransforms.Count; i++)
            {
                Vector3 local = _unitTransforms[i].position - Offset;
                _positions[i] = local;
                int bx = Mathf.Clamp(Mathf.FloorToInt((local.x + 50f) * 0.5f), 0, BucketWidth - 1);
                int bz = Mathf.Clamp(Mathf.FloorToInt((local.z + 30f) * 0.5f), 0, BucketHeight - 1);
                int bucket = bz * BucketWidth + bx;
                _bucketNext[i] = _bucketHeads[bucket];
                _bucketHeads[bucket] = i;
            }

            for (int i = 0; i < _unitTransforms.Count; i++)
            {
                if (!_unitOrderAccepted[i]) continue;
                Vector3 position = _positions[i];
                float step = baseStep * TerrainSpeedAt(Offset + position);
                Vector3 target = _targets[i] - Offset;
                Vector3 direction;
                bool direct = CanFollowStraightRoute(position, target);
                if (direct)
                {
                    direction = target - position;
                    if (direction.sqrMagnitude < 0.2f * 0.2f) continue;
                }
                else direction = GetFlowDirection(position);
                if (direction.sqrMagnitude < 0.00001f) continue;
                direction.Normalize();
                direction += Separation(i, position) * 1.4f;
                if (direction.sqrMagnitude < 0.00001f) continue;
                direction.Normalize();
                float distance = direct ? Mathf.Min(step, (target - position).magnitude) : step;
                Vector3 delta = direction * distance;
                Vector3 next = position + delta;
                if (!CanTravel(position, next))
                {
                    if (_flowFieldDirty)
                    {
                        RefreshFlowTerrainRoutes();
                        return;
                    }
                    Vector3 alongX = position + new Vector3(delta.x, 0, 0);
                    Vector3 alongZ = position + new Vector3(0, 0, delta.z);
                    if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z) && CanTravel(position, alongX)) next = alongX;
                    else if (CanTravel(position, alongZ)) next = alongZ;
                    else if (CanTravel(position, alongX)) next = alongX;
                    else continue;
                }
                _unitTransforms[i].position = Offset + next;
            }
        }

        private Vector3 GetFlowDirection(Vector3 local)
        {
            Vector3 world = Offset + local;
            if (!WorldToCell(world, out int x, out int z)) return Vector3.zero;
            if (_field.IsWall(x, z))
            {
                Vector3 nearest = FindNearestWalkable(world, true);
                return nearest - world;
            }
            FlowVector flow = _field.Vectors[x, z];
            if (flow.IsZero) return Vector3.zero;
            int nextX = x + (int)Mathf.Sign(flow.X) * (flow.X == 0 ? 0 : 1);
            int nextZ = z + (int)Mathf.Sign(flow.Y) * (flow.Y == 0 ? 0 : 1);
            return CellCenter(nextX, nextZ) - world;
        }

        private Vector3 Separation(int index, Vector3 local)
        {
            int bx = Mathf.Clamp(Mathf.FloorToInt((local.x + 50f) * 0.5f), 0, BucketWidth - 1);
            int bz = Mathf.Clamp(Mathf.FloorToInt((local.z + 30f) * 0.5f), 0, BucketHeight - 1);
            Vector3 force = Vector3.zero;
            const float range = 1.05f;
            for (int z = Mathf.Max(0, bz - 1); z <= Mathf.Min(BucketHeight - 1, bz + 1); z++)
            {
                for (int x = Mathf.Max(0, bx - 1); x <= Mathf.Min(BucketWidth - 1, bx + 1); x++)
                {
                    for (int other = _bucketHeads[z * BucketWidth + x]; other >= 0; other = _bucketNext[other])
                    {
                        if (other == index) continue;
                        Vector3 delta = local - _positions[other];
                        float squared = delta.sqrMagnitude;
                        if (squared >= range * range) continue;
                        if (squared < 0.00001f)
                        {
                            float angle = ((index * 37 + other * 17) % 360) * Mathf.Deg2Rad;
                            force += new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                        }
                        else
                        {
                            float distance = Mathf.Sqrt(squared);
                            force += delta / distance * ((range - distance) / range);
                        }
                    }
                }
            }
            return force;
        }

        private Vector3 ResolveSlot(Vector3 world)
        {
            Vector3 local = world - Offset;
            local.x = Mathf.Clamp(local.x, -49.4f, 49.4f);
            local.z = Mathf.Clamp(local.z, -29.4f, 29.4f);
            world = Offset + local;
            if (WorldToCell(world, out int x, out int z) && !_field.IsWall(x, z)) return world;
            return FindNearestWalkable(world, false);
        }

        private Vector3 FormationCenter(Vector3 destination)
        {
            // 先给整队留出空间，避免墙体或地图边缘把大量站位压到同一个点。
            float extent = (Columns - 1) * 0.5f * Spacing;
            Vector3 local = destination - Offset;
            local.x = Mathf.Clamp(local.x, -49f + extent, 49f - extent);
            local.z = Mathf.Clamp(local.z, -29f + extent, 29f - extent);
            float wallClearance = extent + 2f;
            if (Mathf.Abs(local.x) < wallClearance && HasCentralTerrainWall(local.z, extent))
            {
                float side = Mathf.Sign(local.x);
                if (Mathf.Abs(local.x) < 0.01f && _unitTransforms.Count > 0)
                {
                    float total = 0;
                    foreach (Transform unit in _unitTransforms) total += unit.position.x - Offset.x;
                    side = total < 0 ? -1 : 1;
                }
                local.x = side * wallClearance;
            }
            return Offset + local;
        }

        private Vector3 FindNearestWalkable(Vector3 world, bool requireSight)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(world.x - Offset.x + 50f), 0, GridWidth - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(world.z - Offset.z + 30f), 0, GridHeight - 1);
            Vector3 best = world;
            float bestDistance = float.PositiveInfinity;
            for (int radius = 1; radius <= 8; radius++)
            {
                for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(GridHeight - 1, cz + radius); z++)
                {
                    for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(GridWidth - 1, cx + radius); x++)
                    {
                        if (_field.IsWall(x, z) || (requireSight && float.IsInfinity(_field.Integration[x, z]))) continue;
                        Vector3 candidate = CellCenter(x, z);
                        float distance = (candidate - world).sqrMagnitude;
                        if (distance >= bestDistance || (requireSight && !CanTravel(world - Offset, candidate - Offset))) continue;
                        best = candidate;
                        bestDistance = distance;
                    }
                }
                if (!float.IsInfinity(bestDistance)) return best;
            }
            return best;
        }

        private static bool WorldToCell(Vector3 world, out int x, out int z)
        {
            x = Mathf.FloorToInt(world.x - Offset.x + 50f);
            z = Mathf.FloorToInt(world.z - Offset.z + 30f);
            return x >= 0 && x < GridWidth && z >= 0 && z < GridHeight;
        }

        private static Vector3 CellCenter(int x, int z)
        {
            return Offset + new Vector3(x - 49.5f, 0, z - 29.5f);
        }

        private bool CanTravel(Vector3 start, Vector3 end)
        {
            if (Mathf.Abs(end.x) > 50f - UnitRadius || Mathf.Abs(end.z) > 30f - UnitRadius) return false;
            if (_terrainRectanglesRevision != TerrainRevision) BuildTerrainRectangles();
            foreach (TerrainRectangle rectangle in _terrainRectangles)
            {
                if (rectangle.Kind != TerrainKind.Wall) continue;
                float minX = rectangle.X - 50f - UnitRadius;
                float maxX = rectangle.X + rectangle.Width - 50f + UnitRadius;
                float minZ = rectangle.Z - 30f - UnitRadius;
                float maxZ = rectangle.Z + rectangle.Depth - 30f + UnitRadius;
                float enter = 0;
                float exit = 1;
                if (ClipAxis(start.x, end.x - start.x, minX, maxX, ref enter, ref exit) &&
                    ClipAxis(start.z, end.z - start.z, minZ, maxZ, ref enter, ref exit)) return false;
            }
            return true;
        }

        private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.000001f) return start >= min && start <= max;
            float a = (min - start) / delta;
            float b = (max - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a);
            exit = Mathf.Min(exit, b);
            return enter <= exit;
        }

        private void ReleaseWorld()
        {
            ReleaseTerrainResources();
            if (_world != null)
            {
                _world.SetActive(false);
                Destroy(_world);
                _world = null;
            }
            _navUnits.Clear();
            _unitTransforms.Clear();
            _field = null;
            _stressCamera = null;
            _routeTransform = null;
            _routeRenderer = null;
            RenderedRouteCount = 0;
            CompleteRouteCount = 0;
            if (_routeMesh != null)
            {
                Destroy(_routeMesh);
                _routeMesh = null;
            }
            if (_routeMaterial != null)
            {
                Destroy(_routeMaterial);
                _routeMaterial = null;
            }
            if (_navInstance.valid) _navInstance.Remove();
            if (_navData != null)
            {
                Destroy(_navData);
                _navData = null;
            }
        }

        private void OnDestroy()
        {
            NavMesh.onPreUpdate -= GuardPendingTerrainWalls;
            StopAllCoroutines();
            ReleaseWorld();
        }

        private static NavMeshBuildSource Source(Vector3 center, Vector3 size)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                size = size,
                area = 0
            };
        }

        private void VisualBox(string name, Vector3 center, Vector3 size, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_world.transform, false);
            box.transform.position = Offset + center;
            box.transform.localScale = size;
            Tint(box.GetComponent<Renderer>(), color);
        }

        private void Tint(Renderer renderer, Color color)
        {
            renderer.sharedMaterial = _sourceMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
