using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Starter.Extensions.Effects
{
    public enum ParticlePriorityPreset { ForceUpdate, High, Normal }

    public sealed class ParticleSimulationBudgetManager : MonoBehaviour
    {
        public static ParticleSimulationBudgetManager Instance { get; private set; }
        [SerializeField, Min(0.01f)] private float maxFrameBudgetMilliseconds = 2.5f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private Camera priorityCamera;
        private readonly List<ParticleSystemController> systems = new List<ParticleSystemController>();
        private readonly List<ParticleSystemController> normal = new List<ParticleSystemController>();
        private readonly List<ParticleSystemController> forced = new List<ParticleSystemController>();
        private readonly Stopwatch stopwatch = new Stopwatch();
        public int RegisteredCount => systems.Count;
        public int UpdatedNormalParticles { get; private set; }
        public double LastSimulationMilliseconds { get; private set; }
        public float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
        public Camera MainCamera => priorityCamera;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (priorityCamera == null) priorityCamera = Camera.main;
        }

        public static ParticleSimulationBudgetManager CreateInstanceIfNeeded()
        {
            if (Instance == null)
            {
                var existing = FindObjectOfType<ParticleSimulationBudgetManager>();
                if (existing != null) Instance = existing;
                else new GameObject("ParticleSimulationBudgetManager").AddComponent<ParticleSimulationBudgetManager>();
            }
            return Instance;
        }

        public void RegisterSystem(ParticleSystemController controller)
        {
            if (controller != null && !systems.Contains(controller)) systems.Add(controller);
        }
        public void UnregisterSystem(ParticleSystemController controller) { systems.Remove(controller); }

        private void LateUpdate()
        {
            normal.Clear(); forced.Clear();
            systems.RemoveAll(IsMissing);
            foreach (var controller in systems)
            {
                if (!controller.isActiveAndEnabled) continue;
                controller.CalculatePriority(Now, priorityCamera);
                if (controller.PriorityPreset == ParticlePriorityPreset.ForceUpdate) forced.Add(controller);
                else normal.Add(controller);
            }
            normal.Sort(ComparePriority);
            stopwatch.Restart();
            UpdatedNormalParticles = 0;
            foreach (var controller in forced) if (controller != null) controller.SimulateTo(Now);
            foreach (var controller in normal)
            {
                if (stopwatch.Elapsed.TotalMilliseconds >= maxFrameBudgetMilliseconds) break;
                if (controller != null) { controller.SimulateTo(Now); UpdatedNormalParticles++; }
            }
            stopwatch.Stop();
            LastSimulationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        private static int ComparePriority(ParticleSystemController a, ParticleSystemController b) { return b.CalculatedPriority.CompareTo(a.CalculatedPriority); }
        private static bool IsMissing(ParticleSystemController controller) { return controller == null; }
        private void OnDisable()
        {
            foreach (var controller in systems) if (controller != null) controller.ReleaseManager(this);
            systems.Clear(); normal.Clear(); forced.Clear();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
