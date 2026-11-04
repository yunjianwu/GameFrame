using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Starter.Extensions.Spine
{
    public enum SpineSequenceAction { Animation, Delay, WaitForSignal, Invoke }

    [Serializable]
    public sealed class SpineSequenceStep
    {
        public SpineSequenceAction action;
        public SpinePlayer player;
        public string animationName;
        [Min(1)] public int repeatCount = 1;
        public string loopAfter;
        [Min(0)] public float delaySeconds;
        public string signal;
        [Min(0.1f)] public float timeoutSeconds = 15;
        public UnityEvent onExecute = new UnityEvent();
    }

    public sealed class SpineSequence : MonoBehaviour
    {
        public List<SpineSequenceStep> steps = new List<SpineSequenceStep>();
        private Coroutine routine;
        private SpinePlayback playback;
        private readonly List<SpinePlayback> owned = new List<SpinePlayback>();
        private string waitingSignal;
        private bool signalled;
        private int generation;
        public bool IsRunning { get; private set; }
        public string Error { get; private set; }
        public event Action Completed;
        public event Action<string> Failed;

        public void Play()
        {
            Stop();
            Error = null;
            IsRunning = true;
            routine = StartCoroutine(Run(++generation));
        }
        public void Notify(string signal) { if (IsRunning && signal == waitingSignal) signalled = true; }
        public void Stop()
        {
            ++generation;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            CancelOwned();
            waitingSignal = null;
            IsRunning = false;
        }
        private void OnDisable() { Stop(); }

        private IEnumerator Run(int owner)
        {
            yield return null;
            foreach (var step in steps)
            {
                if (step == null) { Fail("Sequence contains a null step."); yield break; }
                float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, step.timeoutSeconds);
                if (step.action == SpineSequenceAction.Animation)
                {
                    if (step.player == null) { Fail("Sequence animation has no SpinePlayer."); yield break; }
                    for (int i = 0; i < Mathf.Max(1, step.repeatCount); i++)
                    {
                        playback = step.player.Play(step.animationName);
                        owned.Add(playback);
                        while (!playback.IsDone && Time.realtimeSinceStartup < deadline) yield return null;
                        if (!playback.IsDone || playback.Result != SpinePlaybackResult.Completed)
                        {
                            playback.Cancel();
                            Fail("Sequence animation timed out, was interrupted or was missing: " + step.animationName);
                            yield break;
                        }
                        if (owner != generation) yield break;
                        deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, step.timeoutSeconds);
                    }
                    if (!string.IsNullOrEmpty(step.loopAfter))
                    {
                        playback = step.player.Play(step.loopAfter, true);
                        owned.Add(playback);
                    }
                }
                else if (step.action == SpineSequenceAction.Delay)
                    yield return new WaitForSecondsRealtime(Mathf.Max(0, step.delaySeconds));
                else if (step.action == SpineSequenceAction.WaitForSignal)
                {
                    waitingSignal = step.signal; signalled = false;
                    while (!signalled && Time.realtimeSinceStartup < deadline) yield return null;
                    waitingSignal = null;
                    if (!signalled) { Fail("Sequence signal timed out: " + step.signal); yield break; }
                }
                step.onExecute?.Invoke();
                if (owner != generation) yield break;
            }
            routine = null;
            IsRunning = false;
            Completed?.Invoke();
        }

        private void Fail(string error)
        {
            routine = null;
            CancelOwned();
            Error = error; IsRunning = false; waitingSignal = null;
            Failed?.Invoke(error);
        }

        private void CancelOwned()
        {
            var snapshot = owned.ToArray();
            owned.Clear(); playback = null;
            foreach (var handle in snapshot) handle.Cancel();
        }
    }
}
