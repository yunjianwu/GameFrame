using System.Collections.Generic;
using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    public sealed class TutorialInputGate : MonoBehaviour
    {
        private readonly HashSet<string> allowed = new HashSet<string>();
        public bool Locked { get; private set; }
        public void SetAllowed(string first, string second = null)
        {
            allowed.Clear();
            if (!string.IsNullOrEmpty(first)) allowed.Add(first);
            if (!string.IsNullOrEmpty(second)) allowed.Add(second);
            Locked = true;
        }
        public bool Allows(string targetId) { return !Locked || allowed.Contains(targetId); }
        public void Unlock() { Locked = false; allowed.Clear(); }
        private void OnDisable() { Unlock(); }
    }
}
