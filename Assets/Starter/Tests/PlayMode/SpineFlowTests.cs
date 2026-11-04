using System.Collections;
using NUnit.Framework;
using Spine.Unity;
using Starter.Extensions.Spine;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Starter.Tests
{
    public sealed class SpineFlowTests
    {
        private GameObject root;
        private TextAsset json;
        private SkeletonDataAsset data;
        private SkeletonGraphic graphic;
        private SpinePlayer player;
        private TextAsset atlasText;
        private SpineAtlasAsset atlas;
        private Material material;
        private Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            json = new TextAsset(@"{""skeleton"":{""hash"":""starter-test"",""spine"":""4.1.00""},""bones"":[{""name"":""root""}],""slots"":[{""name"":""label"",""bone"":""root""}],""animations"":{""show"":{""bones"":{""root"":{""rotate"":[{""value"":0},{""time"":0.2,""value"":10}]}}},""idle"":{""bones"":{""root"":{""rotate"":[{""value"":0},{""time"":1,""value"":0}]}}}}}");
            atlasText = new TextAsset("white.png\nsize: 2,2\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\nwhite\n  rotate: false\n  xy: 0,0\n  size: 2,2\n  orig: 2,2\n  offset: 0,0\n  index: -1\n");
            texture = new Texture2D(2, 2) { name = "white" };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
            material = new Material(Shader.Find("Spine/SkeletonGraphic")) { mainTexture = texture };
            atlas = SpineAtlasAsset.CreateRuntimeInstance(atlasText, new[] { material }, true);
            data = SkeletonDataAsset.CreateRuntimeInstance(json, atlas, true, 1);
            root = new GameObject("spine contracts", typeof(RectTransform), typeof(Canvas), typeof(SkeletonGraphic));
            graphic = root.GetComponent<SkeletonGraphic>();
            graphic.material = material;
            graphic.skeletonDataAsset = data;
            graphic.Initialize(true);
            player = root.AddComponent<SpinePlayer>(); player.Configure(graphic);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root); Object.Destroy(data); Object.Destroy(json);
            Object.Destroy(atlas); Object.Destroy(atlasText); Object.Destroy(material);
            Object.Destroy(texture);
            yield return null;
        }

        [Test]
        public void ReplacementReportsInterruptionAndOldCancellationPreservesNewTrack()
        {
            var first = player.Play("show");
            var second = player.Play("idle", true);
            Assert.AreEqual(SpinePlaybackResult.Interrupted, first.Result);
            first.Cancel();
            Assert.AreSame(second.Entry, player.State.GetCurrent(0));
            Assert.IsFalse(second.IsDone);
            second.Cancel();
            Assert.AreEqual(SpinePlaybackResult.Cancelled, second.Result);
            Assert.IsNull(player.State.GetCurrent(0));
        }

        [Test]
        public void InterruptionCallbackCanStartAnotherPlaybackWithoutLosingTheOuterHandle()
        {
            SpinePlayback inner = null;
            int interruptions = 0;
            player.Play("show", completed: result =>
            {
                if (result != SpinePlaybackResult.Interrupted) return;
                interruptions++;
                inner = player.Play("idle", true);
            });

            var outer = player.Play("show");

            Assert.AreEqual(1, interruptions);
            Assert.AreEqual(SpinePlaybackResult.Interrupted, outer.Result,
                "Reentrant replacement must finish the outer handle before its TrackEntry is recycled.");
            Assert.IsNotNull(inner);
            Assert.IsFalse(inner.IsDone);
            Assert.AreSame(inner.Entry, player.State.GetCurrent(0));
            Assert.AreEqual("idle", player.State.GetCurrent(0).Animation.Name);
            outer.Cancel();
            Assert.AreSame(inner.Entry, player.State.GetCurrent(0));
        }

        [Test]
        public void FinishedSubscriberCanReplaceTheOuterPlaybackSafely()
        {
            SpinePlayback inner = null;
            int finished = 0;
            var first = player.Play("show");
            first.Finished += _ => { finished++; inner = player.Play("idle", true); };

            var outer = player.Play("show");

            Assert.AreEqual(1, finished);
            Assert.AreEqual(SpinePlaybackResult.Interrupted, outer.Result,
                "The public Finished event must obey the same reentrancy boundary as the completion callback.");
            Assert.IsNotNull(inner);
            Assert.IsFalse(inner.IsDone);
            Assert.AreSame(inner.Entry, player.State.GetCurrent(0));
            Assert.AreEqual("idle", player.State.GetCurrent(0).Animation.Name);
        }

        [Test]
        public void StartOffsetIsIndependentFromTrackAndCompletionFiresOnce()
        {
            int completed = 0;
            var playback = player.Play("show", trackIndex: 2, startTimeSeconds: 0.1f,
                completed: result => { if (result == SpinePlaybackResult.Completed) completed++; });
            Assert.AreEqual(2, playback.Entry.TrackIndex);
            Assert.AreEqual(0.1f, playback.Entry.TrackTime, 0.0001f);
            player.State.Update(0.3f); player.State.Apply(player.Skeleton);
            player.State.Update(0.3f); player.State.Apply(player.Skeleton);
            Assert.AreEqual(SpinePlaybackResult.Completed, playback.Result);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void CancelQueuedAnimationRemovesQueueTail()
        {
            var first = player.Play("show");
            var queued = player.Queue("idle");
            Assert.AreSame(queued.Entry, first.Entry.Next);
            queued.Cancel();
            Assert.IsNull(first.Entry.Next);
            Assert.AreSame(first.Entry, player.State.GetCurrent(0));
        }

        [Test]
        public void SlotAlphaIsAppliedExactlyOnceToUi()
        {
            var textObject = new GameObject("dynamic label", typeof(RectTransform), typeof(Text), typeof(CanvasGroup));
            textObject.transform.SetParent(root.transform, false);
            textObject.GetComponent<Text>().color = Color.white;
            var follower = textObject.AddComponent<SlotAlphaFollowerGraphic>();
            follower.skeletonGraphic = graphic; follower.slotName = "label";
            graphic.Skeleton.FindSlot("label").A = 0.5f;
            follower.Apply();
            Assert.AreEqual(0.5f, textObject.GetComponent<CanvasGroup>().alpha, 0.0001f);
            Assert.AreEqual(1, textObject.GetComponent<Text>().color.a, 0.0001f);
        }

        [Test]
        public void MissingIntroFallsBackToIdleWithoutThrowing()
        {
            var intro = root.AddComponent<SpineIntroLoop>();
            intro.Configure(player, "not present", "idle");
            Assert.DoesNotThrow(() => intro.Play());
            Assert.AreEqual("idle", player.State.GetCurrent(0).Animation.Name);
            Assert.AreEqual(SpinePlaybackResult.MissingAnimation, player.Play("not present").Result);
        }

        [UnityTest]
        public IEnumerator SequenceStopsWithErrorWhenAnimationIsInterrupted()
        {
            var sequence = root.AddComponent<SpineSequence>();
            sequence.steps.Add(new SpineSequenceStep { action = SpineSequenceAction.Animation, player = player, animationName = "show" });
            sequence.Play();
            yield return null;
            yield return null;
            player.Play("idle", true);
            yield return null;
            yield return null;
            Assert.IsFalse(sequence.IsRunning);
            Assert.IsNotEmpty(sequence.Error);
        }

        [UnityTest]
        public IEnumerator FailedSequenceCancelsLoopsStartedByEarlierSteps()
        {
            var sequence = root.AddComponent<SpineSequence>();
            sequence.steps.Add(new SpineSequenceStep { player = player, animationName = "show", loopAfter = "idle" });
            sequence.steps.Add(new SpineSequenceStep { player = player, animationName = "missing" });
            sequence.Play();
            float deadline = Time.realtimeSinceStartup + 2;
            while (sequence.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(sequence.IsRunning);
            Assert.IsNotEmpty(sequence.Error);
            Assert.IsNull(player.State.GetCurrent(0), "A failed sequence must release loops it started earlier.");
        }
    }
}
