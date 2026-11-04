using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Extensions.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialRunner : MonoBehaviour
    {
        [SerializeField] private TutorialConfig config;
        [SerializeField] private TutorialTargetRegistry registry;
        [SerializeField] private TutorialMask mask;
        [SerializeField] private FingerPointer finger;
        [SerializeField] private TutorialInputGate inputGate;
        [SerializeField] private GameObject talkRoot;
        [SerializeField] private Text talkText;
        private ITutorialProgressStore store;
        private readonly HashSet<string> signals = new HashSet<string>(StringComparer.Ordinal);
        private Coroutine routine;
        private TutorialTarget boundTarget;
        private string dragDestination;
        private bool advance;
        private bool completionSignaled;
        private int generation;
        private int index;

        public TutorialState State { get; private set; } = TutorialState.Stopped;
        public string Error { get; private set; }
        public TutorialStep CurrentStep => config != null && config.steps != null && index >= 0 && index < config.steps.Count ? config.steps[index] : null;
        public event Action<string> StepEntered;
        public event Action<string> StepCompleted;
        public event Action Completed;
        public event Action<string> Failed;

        public void Configure(TutorialConfig definition, TutorialTargetRegistry targets, ITutorialProgressStore progress = null)
        {
            Stop();
            config = definition;
            registry = targets;
            store = progress;
        }

        public void ConfigurePresentation(TutorialMask overlay, FingerPointer pointer, TutorialInputGate gate = null, GameObject dialogue = null, Text text = null)
        {
            HidePresentation();
            mask = overlay; finger = pointer; inputGate = gate; talkRoot = dialogue; talkText = text;
            HidePresentation();
        }

        public void Begin(bool resume = true)
        {
            Stop();
            if (!isActiveAndEnabled) throw new InvalidOperationException("Enable TutorialRunner before Begin.");
            if (config == null || registry == null) throw new InvalidOperationException("Configure a tutorial and target registry first.");
            config.Validate();
            signals.Clear();
            Error = null;
            index = 0;
            if (resume && store != null && store.TryLoad(config.tutorialId, out string next))
            {
                if (string.IsNullOrEmpty(next)) { Finish(); return; }
                int savedIndex = config.steps.FindIndex(step => step.id == next);
                if (savedIndex < 0) { Fail("Saved step ID does not exist: " + next); return; }
                index = savedIndex;
            }
            if (index >= config.steps.Count) { Finish(); return; }
            int current = ++generation;
            State = TutorialState.WaitingForCondition;
            routine = StartCoroutine(Run(current));
        }

        public void Notify(string signal)
        {
            if (string.IsNullOrEmpty(signal) || State == TutorialState.Stopped || State == TutorialState.Completed || State == TutorialState.Failed) return;
            signals.Add(signal);
            var step = CurrentStep;
            if (step != null && step.type == TutorialStepType.Signal && step.completionSignal == signal)
            {
                completionSignaled = true;
                if (State == TutorialState.Running) advance = true;
            }
        }

        public bool SkipCurrent()
        {
            if (CurrentStep == null || !CurrentStep.skippable || State == TutorialState.Stopped || State == TutorialState.Completed || State == TutorialState.Failed) return false;
            advance = true;
            return true;
        }

        public void Stop()
        {
            ++generation;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Unbind();
            HidePresentation();
            advance = false;
            completionSignaled = false;
            State = TutorialState.Stopped;
        }

        private void OnDisable() { Stop(); }

        private IEnumerator Run(int owner)
        {
            // Let Begin assign the coroutine handle before any completion callback can re-enter.
            yield return null;
            while (owner == generation && index < config.steps.Count)
            {
                var step = CurrentStep;
                advance = false;
                completionSignaled = false;
                store?.Save(config.tutorialId, step.id);
                StepEntered?.Invoke(step.id);
                if (owner != generation) yield break;
                float deadline = Time.realtimeSinceStartup + step.targetTimeoutSeconds;
                bool completed = false;
                while (owner == generation && !completed)
                {
                    State = TutorialState.WaitingForCondition;
                    while (!advance && !string.IsNullOrEmpty(step.prerequisiteSignal) && !signals.Contains(step.prerequisiteSignal))
                    {
                        if (Expired(deadline, step)) yield break;
                        yield return null;
                    }
                    TutorialTarget a = null, b = null;
                    State = TutorialState.WaitingForTarget;
                    while (!advance && !Resolve(step, out a, out b))
                    {
                        if (Expired(deadline, step)) yield break;
                        yield return null;
                    }
                    if (advance) { completed = true; break; }
                    State = TutorialState.Presenting;
                    if (a != null)
                    {
                        if (mask != null) mask.SetTargets(a.Rect, b == null ? null : b.Rect);
                        if (inputGate != null) inputGate.SetAllowed(a.Id, b == null ? null : b.Id);
                    }
                    if (talkText != null) talkText.text = step.talkText ?? string.Empty;
                    if (talkRoot != null) talkRoot.SetActive(!string.IsNullOrEmpty(step.talkText));
                    float showAt = Time.realtimeSinceStartup + Mathf.Max(0, step.fingerDelaySeconds);
                    while (!advance && Time.realtimeSinceStartup < showAt && Available(a, b)) yield return null;
                    if (!Available(a, b))
                    {
                        HidePresentation();
                        deadline = Time.realtimeSinceStartup + step.targetTimeoutSeconds;
                        continue;
                    }
                    if (finger != null && a != null)
                    {
                        if (b == null) finger.PointTo(a.Rect);
                        else finger.SetRepeatBetween(a.Rect, b.Rect, 0.7f, 0.25f);
                    }
                    Bind(step, a);
                    State = TutorialState.Running;
                    if (step.type == TutorialStepType.Signal && completionSignaled) advance = true;
                    float finishAt = Time.realtimeSinceStartup + Mathf.Max(0, step.durationSeconds);
                    while (!advance && Available(a, b))
                    {
                        if (step.type == TutorialStepType.Delay && Time.realtimeSinceStartup >= finishAt) advance = true;
                        if (!advance) yield return null;
                    }
                    completed = advance;
                    Unbind();
                    HidePresentation();
                    deadline = Time.realtimeSinceStartup + step.targetTimeoutSeconds;
                }
                if (owner != generation) yield break;
                Unbind();
                HidePresentation();
                ++index;
                store?.Save(config.tutorialId, index < config.steps.Count ? config.steps[index].id : string.Empty);
                StepCompleted?.Invoke(step.id);
            }
            if (owner == generation) Finish();
        }

        private bool Resolve(TutorialStep step, out TutorialTarget a, out TutorialTarget b)
        {
            a = b = null;
            if (!string.IsNullOrEmpty(step.targetId) && !registry.TryGet(step.targetId, out a)) return false;
            return string.IsNullOrEmpty(step.secondTargetId) || registry.TryGet(step.secondTargetId, out b);
        }

        private static bool Available(TutorialTarget a, TutorialTarget b)
        {
            // ReferenceEquals distinguishes an unconfigured target from a destroyed Unity object.
            return (ReferenceEquals(a, null) || (a != null && a.isActiveAndEnabled)) &&
                   (ReferenceEquals(b, null) || (b != null && b.isActiveAndEnabled));
        }

        private void Bind(TutorialStep step, TutorialTarget target)
        {
            Unbind();
            if (target == null) return;
            boundTarget = target;
            if (step.type == TutorialStepType.Click) target.Clicked += OnClick;
            if (step.type == TutorialStepType.Drag) { dragDestination = step.secondTargetId; target.DraggedTo += OnDrag; }
        }

        private void Unbind()
        {
            if (boundTarget != null) { boundTarget.Clicked -= OnClick; boundTarget.DraggedTo -= OnDrag; }
            boundTarget = null;
            dragDestination = null;
        }

        private void OnClick() { if (State == TutorialState.Running) advance = true; }
        private void OnDrag(string destination) { if (State == TutorialState.Running && destination == dragDestination) advance = true; }

        private bool Expired(float deadline, TutorialStep step)
        {
            if (Time.realtimeSinceStartup < deadline) return false;
            Fail("Timed out waiting for tutorial step: " + step.id);
            return true;
        }

        private void HidePresentation()
        {
            if (mask != null) mask.Hide();
            if (finger != null) finger.Hide();
            if (inputGate != null) inputGate.Unlock();
            if (talkRoot != null) talkRoot.SetActive(false);
        }

        private void Finish()
        {
            routine = null;
            Unbind(); HidePresentation();
            State = TutorialState.Completed;
            Completed?.Invoke();
        }

        private void Fail(string error)
        {
            routine = null;
            Unbind(); HidePresentation();
            Error = error;
            State = TutorialState.Failed;
            Failed?.Invoke(error);
        }
    }
}
