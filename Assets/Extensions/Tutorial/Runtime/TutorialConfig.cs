using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    public enum TutorialStepType { Click, Drag, Signal, Delay }
    public enum TutorialState { Stopped, WaitingForCondition, WaitingForTarget, Presenting, Running, Completed, Failed }

    [Serializable]
    public sealed class TutorialStep
    {
        [Tooltip("Stable save ID. Keep it unchanged when reordering steps.")]
        public string id = "step";
        public TutorialStepType type;
        public string targetId;
        public string secondTargetId;
        [Tooltip("Signal to complete this step. Gameplay reports it after its own validation.")]
        public string completionSignal;
        [Tooltip("Optional prerequisite signal. Signals received earlier in this run are remembered.")]
        public string prerequisiteSignal;
        [TextArea] public string talkText;
        public bool skippable;
        [Min(0)] public float fingerDelaySeconds = 0.2f;
        [Min(0.05f)] public float targetTimeoutSeconds = 15;
        [Min(0)] public float durationSeconds = 1;
    }

    [CreateAssetMenu(menuName = "Starter/Tutorial Config", fileName = "TutorialConfig")]
    public sealed class TutorialConfig : ScriptableObject
    {
        public string tutorialId = "tutorial.example";
        public List<TutorialStep> steps = new List<TutorialStep>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(tutorialId)) throw new InvalidOperationException("Tutorial ID is required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (steps == null) throw new InvalidOperationException("Tutorial steps cannot be null.");
            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id))
                    throw new InvalidOperationException("Every tutorial step needs a unique stable ID.");
                if ((step.type == TutorialStepType.Click || step.type == TutorialStepType.Drag) && string.IsNullOrWhiteSpace(step.targetId))
                    throw new InvalidOperationException("Click and drag steps need a target ID.");
                if (step.type == TutorialStepType.Drag && string.IsNullOrWhiteSpace(step.secondTargetId))
                    throw new InvalidOperationException("Drag steps need a destination ID.");
                if (step.type == TutorialStepType.Signal && string.IsNullOrWhiteSpace(step.completionSignal))
                    throw new InvalidOperationException("Signal steps need a completion signal.");
                if (step.targetTimeoutSeconds <= 0 || float.IsNaN(step.targetTimeoutSeconds) || float.IsInfinity(step.targetTimeoutSeconds))
                    throw new InvalidOperationException("Target timeout must be finite and positive.");
            }
        }
    }
}
