using System.Collections.Generic;
using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    public interface ITutorialProgressStore
    {
        bool TryLoad(string tutorialId, out string nextStepId);
        void Save(string tutorialId, string nextStepId);
    }

    public sealed class MemoryTutorialProgressStore : ITutorialProgressStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public bool TryLoad(string tutorialId, out string nextStepId) { return values.TryGetValue(tutorialId, out nextStepId); }
        public void Save(string tutorialId, string nextStepId) { values[tutorialId] = nextStepId; }
    }

    // An empty saved step ID means the entire tutorial is complete.
    public sealed class PlayerPrefsTutorialProgressStore : ITutorialProgressStore
    {
        private readonly string prefix;
        public PlayerPrefsTutorialProgressStore(string keyPrefix = "starter.tutorial.") { prefix = keyPrefix; }
        public bool TryLoad(string tutorialId, out string nextStepId)
        {
            string key = prefix + tutorialId;
            nextStepId = PlayerPrefs.GetString(key, string.Empty);
            return PlayerPrefs.HasKey(key);
        }
        public void Save(string tutorialId, string nextStepId)
        {
            PlayerPrefs.SetString(prefix + tutorialId, nextStepId);
            PlayerPrefs.Save();
        }
    }
}
