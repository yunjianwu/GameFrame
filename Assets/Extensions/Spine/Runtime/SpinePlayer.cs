using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = global::Spine.AnimationState;

namespace Starter.Extensions.Spine
{
    [DisallowMultipleComponent]
    public sealed class SpinePlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("SkeletonGraphic or SkeletonAnimation on this object or another object.")]
        private Component source;
        private readonly List<SpinePlayback> active = new List<SpinePlayback>();
        private readonly Queue<Action> completionCallbacks = new Queue<Action>();
        private int mutationDepth;
        private bool dispatchingCallbacks;
        private IAnimationStateComponent animationSource;
        private ISkeletonComponent skeletonSource;

        public AnimationState State { get { Resolve(); return animationSource?.AnimationState; } }
        public Skeleton Skeleton { get { Resolve(); return skeletonSource?.Skeleton; } }
        public Component Source => source;

        public void Configure(Component component)
        {
            CancelAll();
            if (component != null && (!(component is IAnimationStateComponent) || !(component is ISkeletonComponent)))
                throw new ArgumentException("Expected SkeletonGraphic or SkeletonAnimation.", nameof(component));
            source = component;
            animationSource = null;
            skeletonSource = null;
            Resolve();
        }

        private void Resolve()
        {
            if (source == null)
            {
                source = GetComponent<SkeletonGraphic>();
                if (source == null) source = GetComponent<SkeletonAnimation>();
            }
            if (source == null) { animationSource = null; skeletonSource = null; return; }
            if (source is SkeletonGraphic graphic && graphic.AnimationState == null) graphic.Initialize(false);
            if (source is SkeletonAnimation animation && animation.state == null) animation.Initialize(false);
            animationSource = source as IAnimationStateComponent;
            skeletonSource = source as ISkeletonComponent;
        }

        public bool HasAnimation(string name) { return !string.IsNullOrEmpty(name) && Skeleton?.Data.FindAnimation(name) != null; }
        public float GetDuration(string name) { return Skeleton?.Data.FindAnimation(name)?.Duration ?? 0; }

        public SpinePlayback Play(string name, bool loop = false, int trackIndex = 0, float startTimeSeconds = 0,
            bool reverse = false, float segmentStartSeconds = 0, float segmentEndSeconds = -1,
            Action<SpinePlaybackResult> completed = null)
        {
            if (trackIndex < 0) throw new ArgumentOutOfRangeException(nameof(trackIndex));
            var state = State;
            var animation = string.IsNullOrEmpty(name) ? null : Skeleton?.Data.FindAnimation(name);
            if (state == null || animation == null) return new SpinePlayback(state, null, completed, DispatchCompletion);
            if (float.IsNaN(startTimeSeconds) || float.IsInfinity(startTimeSeconds) || startTimeSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(startTimeSeconds));
            float start = Mathf.Clamp(segmentStartSeconds, 0, animation.Duration);
            float end = segmentEndSeconds < 0 ? animation.Duration : Mathf.Clamp(segmentEndSeconds, start, animation.Duration);
            ++mutationDepth;
            try
            {
                var entry = state.SetAnimation(trackIndex, animation, loop);
                entry.AnimationStart = start;
                entry.AnimationEnd = end;
                entry.AnimationLast = start;
                entry.TrackTime = loop ? startTimeSeconds : Mathf.Min(startTimeSeconds, end - start);
                entry.Reverse = reverse;
                return Track(state, entry, completed);
            }
            finally { --mutationDepth; FlushCompletions(); }
        }

        public SpinePlayback Queue(string name, bool loop = false, int trackIndex = 0, float delaySeconds = 0,
            Action<SpinePlaybackResult> completed = null)
        {
            if (trackIndex < 0) throw new ArgumentOutOfRangeException(nameof(trackIndex));
            var state = State;
            var animation = string.IsNullOrEmpty(name) ? null : Skeleton?.Data.FindAnimation(name);
            if (state == null || animation == null) return new SpinePlayback(state, null, completed, DispatchCompletion);
            ++mutationDepth;
            try { return Track(state, state.AddAnimation(trackIndex, animation, loop, delaySeconds), completed); }
            finally { --mutationDepth; FlushCompletions(); }
        }

        public SpinePlayback PlayLoop(string name, bool includeStartAnimation = true)
        {
            string start = name + "_start";
            if (includeStartAnimation && HasAnimation(start))
            {
                Play(start);
                return Queue(name, true);
            }
            return Play(name, true);
        }

        public bool SetSkin(string name)
        {
            var skeleton = Skeleton;
            if (skeleton == null || string.IsNullOrEmpty(name) || skeleton.Data.FindSkin(name) == null) return false;
            skeleton.SetSkin(name);
            skeleton.SetSlotsToSetupPose();
            State.Apply(skeleton);
            skeleton.UpdateWorldTransform();
            return true;
        }

        public void SetSpeed(float speed)
        {
            if (speed < 0 || float.IsNaN(speed) || float.IsInfinity(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            if (State != null) State.TimeScale = speed;
        }

        private SpinePlayback Track(AnimationState state, TrackEntry entry, Action<SpinePlaybackResult> callback)
        {
            var playback = new SpinePlayback(state, entry, callback, DispatchCompletion, OnFinished);
            active.Add(playback);
            return playback;
        }

        private void DispatchCompletion(Action callback)
        {
            completionCallbacks.Enqueue(callback);
            FlushCompletions();
        }

        private void FlushCompletions()
        {
            // Spine drains interruption events inside SetAnimation. Subscribe the new handle
            // before user callbacks can replace its entry and return it to Spine's pool.
            if (mutationDepth != 0 || dispatchingCallbacks) return;
            dispatchingCallbacks = true;
            try
            {
                while (completionCallbacks.Count > 0) completionCallbacks.Dequeue()();
            }
            finally { dispatchingCallbacks = false; }
        }

        private void OnFinished(SpinePlayback playback) { active.Remove(playback); }
        public void CancelAll()
        {
            // Cancellation callbacks may start another animation. Only cancel the handles owned at entry.
            var snapshot = active.ToArray();
            foreach (var playback in snapshot) playback.Cancel();
        }
        private void OnDisable() { CancelAll(); }
        private void OnDestroy() { CancelAll(); }
    }
}
