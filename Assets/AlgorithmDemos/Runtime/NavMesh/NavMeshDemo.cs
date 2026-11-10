using System.Collections.Generic;
using Starter.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AlgorithmDemos.NavMeshExample
{
    public sealed class NavMeshDemo : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Renderer _ground;
        [SerializeField] private Renderer[] _walls;
        [SerializeField] private NavMeshUnitMover[] _units;

        private readonly List<NavMeshUnitMover> _selected = new List<NavMeshUnitMover>();
        private LineRenderer _line;
        private Material _lineMaterial;
        private string _status = "选择单位后右键移动";

        private void Start()
        {
            Tint(_ground, new Color32(66, 100, 82, 255));
            foreach (Renderer wall in _walls) Tint(wall, new Color32(169, 110, 69, 255));
            if (_units.Length > 0) _selected.Add(_units[0]);
            RefreshSelection();

            var lineObject = new GameObject("Selected Path");
            lineObject.transform.SetParent(transform, false);
            _line = lineObject.AddComponent<LineRenderer>();
            _line.startWidth = _line.endWidth = 0.1f;
            _line.positionCount = 0;
            _line.startColor = _line.endColor = new Color32(239, 235, 136, 255);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _lineMaterial = new Material(shader);
                _line.sharedMaterial = _lineMaterial;
            }
            // 群体移动演示随当前业务场景创建和销毁，不依赖本地验收脚本。
            if (GetComponent<NavMeshPackageStress>() == null) gameObject.AddComponent<NavMeshPackageStress>();
        }

        private void Update()
        {
            Vector2 mouse = Input.mousePosition;
            bool overPanel = mouse.x < 360 && mouse.y > Screen.height - 220 && mouse.y < Screen.height - 70;
            if (!overPanel && !DemoInputGate.IsBlocked(mouse))
            {
                if (Input.GetMouseButtonDown(0)) SelectUnit(mouse);
                if (Input.GetMouseButtonDown(1)) MoveUnits(mouse);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                foreach (NavMeshUnitMover unit in _selected) unit.Stop();
                _status = "已发送停止指令";
            }
            UpdatePathLine();
        }

        private void OnGUI()
        {
            DemoPresentation.BeginGui();
            GUI.Box(new Rect(12, 76, 340, 136),
                "UNITY NAVMESH  /  单位移动\n\n左键选择单位，Shift + 左键多选\n右键设置目的地，S 停止\n\n" + _status);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private void SelectUnit(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200)) return;
            NavMeshUnitMover unit = hit.collider.GetComponentInParent<NavMeshUnitMover>();
            if (unit == null) return;

            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!additive) _selected.Clear();
            if (additive && _selected.Contains(unit)) _selected.Remove(unit);
            else _selected.Add(unit);
            _status = "已选择 " + _selected.Count + " 个单位";
            RefreshSelection();
        }

        private void MoveUnits(Vector2 screenPosition)
        {
            if (_selected.Count == 0) return;
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!groundPlane.Raycast(ray, out float distance)) return;
            Vector3 destination = ray.GetPoint(distance);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(_selected.Count));
            int rows = Mathf.CeilToInt((float)_selected.Count / columns);
            int accepted = 0;
            for (int i = 0; i < _selected.Count; i++)
            {
                float x = (i % columns - (columns - 1) * 0.5f) * 1.3f;
                float z = (i / columns - (rows - 1) * 0.5f) * 1.3f;
                if (_selected[i].MoveTo(destination + new Vector3(x, 0, z)) == MoveRequestResult.Accepted)
                    accepted++;
            }
            _status = "接受移动：" + accepted + " / " + _selected.Count + "（其他目标不可达或无效）";
        }

        private void RefreshSelection()
        {
            foreach (NavMeshUnitMover unit in _units)
                Tint(unit.GetComponentInChildren<Renderer>(), _selected.Contains(unit)
                    ? new Color32(238, 221, 111, 255)
                    : new Color32(91, 165, 213, 255));
        }

        private void UpdatePathLine()
        {
            if (_selected.Count == 0 || !_selected[0].Agent.isOnNavMesh || !_selected[0].Agent.hasPath)
            {
                _line.positionCount = 0;
                return;
            }
            Vector3[] corners = _selected[0].Agent.path.corners;
            _line.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++) _line.SetPosition(i, corners[i] + Vector3.up * 0.12f);
        }

        private static void Tint(Renderer target, Color color)
        {
            if (target == null) return;
            var properties = new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            properties.SetColor("_Color", color);
            target.SetPropertyBlock(properties);
        }
    }
}
