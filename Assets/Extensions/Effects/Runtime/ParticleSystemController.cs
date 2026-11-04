using UnityEngine;

namespace Starter.Extensions.Effects
{
    [RequireComponent(typeof(ParticleSystem)), DisallowMultipleComponent]
    public sealed class ParticleSystemController : MonoBehaviour
    {
        [SerializeField] private ParticlePriorityPreset priorityPreset = ParticlePriorityPreset.Normal;
        [SerializeField, Min(0)] private float basePriority = 1;
        [SerializeField, Min(0)] private float distanceFactor = 1;
        private ParticleSystem particles;
        private Renderer particleRenderer;
        private ParticleSimulationBudgetManager manager;
        private float lastSimulated;
        private bool wasPlaying;
        private bool wasPaused;
        private bool ownsSimulation;
        public ParticlePriorityPreset PriorityPreset { get => priorityPreset; set => priorityPreset = value; }
        public float CalculatedPriority { get; private set; }

        private void Awake() { particles = GetComponent<ParticleSystem>(); particleRenderer = GetComponent<Renderer>(); }
        private void OnEnable() { Attach(); }
        private void Update() { if (manager == null || !manager.isActiveAndEnabled) Attach(); }

        private void Attach()
        {
            var candidate = ParticleSimulationBudgetManager.CreateInstanceIfNeeded();
            if (candidate == null || !candidate.isActiveAndEnabled) return;
            if (manager == candidate) return;
            if (particles == null) particles = GetComponent<ParticleSystem>();
            manager = candidate;
            wasPlaying = particles.isPlaying;
            wasPaused = particles.isPaused;
            ownsSimulation = true;
            lastSimulated = manager.Now;
            particles.Pause(false);
            manager.RegisterSystem(this);
        }

        internal void CalculatePriority(float now, Camera camera)
        {
            float age = Mathf.Max(0, now - lastSimulated);
            float distanceWeight = 1;
            if (camera != null) distanceWeight = 1 / (1 + (transform.position - camera.transform.position).sqrMagnitude * 0.1f);
            float compensation = Mathf.Log(1 + age) * (priorityPreset == ParticlePriorityPreset.High ? 2 : 1);
            CalculatedPriority = basePriority + compensation * distanceWeight * distanceFactor;
            if (particleRenderer != null && !particleRenderer.isVisible) CalculatedPriority *= 0.3f;
        }

        internal void SimulateTo(float now)
        {
            if (particles == null || !isActiveAndEnabled) return;
            float delta = Mathf.Max(0, now - lastSimulated);
            if (delta > 0 && wasPlaying) particles.Simulate(delta, false, false, false);
            lastSimulated = now;
        }

        internal void ReleaseManager(ParticleSimulationBudgetManager owner)
        {
            if (manager != owner) return;
            manager = null;
            RestoreAutomaticSimulation();
        }

        private void RestoreAutomaticSimulation()
        {
            if (particles == null || !ownsSimulation) return;
            ownsSimulation = false;
            if (wasPlaying) particles.Play(false);
            else if (wasPaused) particles.Pause(false);
            else particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDisable()
        {
            if (manager != null) manager.UnregisterSystem(this);
            manager = null;
            RestoreAutomaticSimulation();
        }
    }
}
