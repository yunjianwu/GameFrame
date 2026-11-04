using System;
using Spine;

namespace Starter.Extensions.Spine
{
    public enum SpinePlaybackResult { Playing, Completed, Interrupted, Cancelled, MissingAnimation }

    public sealed class SpinePlayback : IDisposable
    {
        private readonly AnimationState state;
        private readonly Action<SpinePlaybackResult> callback;
        private readonly Action<Action> dispatch;
        private readonly Action<SpinePlayback> release;
        public TrackEntry Entry { get; }
        public SpinePlaybackResult Result { get; private set; }
        public bool IsDone => Result != SpinePlaybackResult.Playing;
        public event Action<SpinePlayback> Finished;
        public event Action CycleCompleted;
        public event Action<global::Spine.Event> AnimationEvent;

        internal SpinePlayback(AnimationState state, TrackEntry entry, Action<SpinePlaybackResult> completion,
            Action<Action> dispatcher, Action<SpinePlayback> releaseHandle = null)
        {
            this.state = state;
            callback = completion;
            dispatch = dispatcher;
            release = releaseHandle;
            Entry = entry;
            Result = entry == null ? SpinePlaybackResult.MissingAnimation : SpinePlaybackResult.Playing;
            if (entry == null) { if (callback != null) dispatch(() => callback(Result)); return; }
            entry.Complete += OnComplete;
            entry.Interrupt += OnInterrupted;
            entry.End += OnInterrupted;
            entry.Dispose += OnInterrupted;
            entry.Event += OnEvent;
        }

        private void OnComplete(TrackEntry entry)
        {
            if (entry.Loop) CycleCompleted?.Invoke();
            else Finish(SpinePlaybackResult.Completed, false);
        }
        private void OnInterrupted(TrackEntry entry) { Finish(SpinePlaybackResult.Interrupted, false); }
        private void OnEvent(TrackEntry entry, global::Spine.Event data) { AnimationEvent?.Invoke(data); }

        public void Cancel() { Finish(SpinePlaybackResult.Cancelled, true); }
        public void Dispose() { Cancel(); }

        private void Finish(SpinePlaybackResult result, bool clearOwnedTrack)
        {
            if (IsDone) return;
            Result = result;
            Entry.Complete -= OnComplete;
            Entry.Interrupt -= OnInterrupted;
            Entry.End -= OnInterrupted;
            Entry.Dispose -= OnInterrupted;
            Entry.Event -= OnEvent;
            if (clearOwnedTrack)
            {
                if (state.GetCurrent(Entry.TrackIndex) == Entry) state.ClearTrack(Entry.TrackIndex);
                else if (Entry.Previous != null && Entry.Previous.Next == Entry) state.ClearNext(Entry.Previous);
            }
            release?.Invoke(this);
            var finished = Finished;
            Finished = null; CycleCompleted = null; AnimationEvent = null;
            if (finished != null || callback != null)
                dispatch(() => { finished?.Invoke(this); callback?.Invoke(result); });
        }
    }
}
