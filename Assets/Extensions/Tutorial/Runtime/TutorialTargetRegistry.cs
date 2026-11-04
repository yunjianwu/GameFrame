using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    public sealed class TutorialTargetRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, TutorialTarget> targets = new Dictionary<string, TutorialTarget>(StringComparer.Ordinal);

        public void Register(TutorialTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Id)) throw new ArgumentException("Target needs a stable ID.");
            if (targets.TryGetValue(target.Id, out var previous) && previous != null && previous != target)
                throw new InvalidOperationException("Duplicate tutorial target ID: " + target.Id);
            targets[target.Id] = target;
        }

        public void Unregister(TutorialTarget target)
        {
            if (target != null && targets.TryGetValue(target.Id, out var current) && current == target) targets.Remove(target.Id);
        }

        public bool TryGet(string id, out TutorialTarget target)
        {
            target = null;
            return !string.IsNullOrEmpty(id) && targets.TryGetValue(id, out target) && target != null && target.isActiveAndEnabled;
        }
    }
}
