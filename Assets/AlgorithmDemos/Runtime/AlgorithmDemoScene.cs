using System;
using UnityEngine;

namespace AlgorithmDemos
{
    [DisallowMultipleComponent]
    public sealed class AlgorithmDemoScene : MonoBehaviour
    {
        [SerializeField] private DemoKind _kind;
        [SerializeField] private MonoBehaviour _simulation;
        public static AlgorithmDemoScene Current { get; private set; }
        public DemoKind Kind => _kind;
        public MonoBehaviour Simulation => _simulation;
        public int ActiveFrames { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != this)
                throw new InvalidOperationException("Only one algorithm demo may own a business scene.");
            Current = this;
        }

        private void Update()
        {
            if (_simulation != null && _simulation.isActiveAndEnabled) ActiveFrames++;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
