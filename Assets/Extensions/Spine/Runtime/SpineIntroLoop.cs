using System.Collections;
using UnityEngine;

namespace Starter.Extensions.Spine
{
    [RequireComponent(typeof(SpinePlayer)), DisallowMultipleComponent]
    public sealed class SpineIntroLoop : MonoBehaviour
    {
        [SerializeField] private SpinePlayer player;
        [SerializeField] private string intro = "show";
        [SerializeField] private string idle = "idle";
        private SpinePlayback playback;
        private Coroutine routine;

        public void Configure(SpinePlayer source, string introName = "show", string idleName = "idle")
        {
            player = source; intro = introName; idle = idleName;
        }
        private void OnEnable() { routine = StartCoroutine(AfterVisible()); }
        private IEnumerator AfterVisible() { yield return null; routine = null; Play(); }
        public void Play()
        {
            playback?.Cancel();
            if (player == null) player = GetComponent<SpinePlayer>();
            if (player == null) return;
            if (!player.HasAnimation(intro)) { PlayIdle(); return; }
            playback = player.Play(intro, completed: result => { if (result == SpinePlaybackResult.Completed && isActiveAndEnabled) PlayIdle(); });
        }
        private void PlayIdle() { if (player.HasAnimation(idle)) playback = player.Play(idle, true); }
        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            playback?.Cancel(); playback = null;
        }
    }
}
