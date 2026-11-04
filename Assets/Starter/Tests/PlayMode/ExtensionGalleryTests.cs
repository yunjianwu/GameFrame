using System;
using System.Collections;
using NUnit.Framework;
using Starter.Extensions.Spine;
using Starter.Extensions.Tutorial;
using Starter.Extensions.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Starter.Tests
{
    public sealed class ExtensionGalleryTests
    {
        [UnityTest]
        public IEnumerator GalleryRunsTutorialListsSpineAndParticlesTogether()
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded("ExtensionGallery"), "Generate and register the extension gallery scene.");
            yield return SceneManager.LoadSceneAsync("ExtensionGallery");
            yield return null;
            GameObject.Find("StartGuide").GetComponent<Button>().onClick.Invoke();
            var runner = UnityEngine.Object.FindObjectOfType<TutorialRunner>();
            yield return WaitFor(() => runner.State == TutorialState.Running);
            GameObject.Find("ConfirmTarget").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => runner.CurrentStep.id == "drag" && runner.State == TutorialState.Running);
            Canvas.ForceUpdateCanvases();
            var source = GameObject.Find("DragSource").GetComponent<TutorialTarget>();
            var destination = GameObject.Find("DragDestination").GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, destination.TransformPoint(destination.rect.center))
            };
            source.OnEndDrag(pointer);
            yield return WaitFor(() => runner.CurrentStep.id == "reward" && runner.State == TutorialState.Running);
            GameObject.Find("BusinessTarget").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => runner.State == TutorialState.Completed);
            GameObject.Find("NavLists").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(4, UnityEngine.Object.FindObjectsOfType<UILoopScrollView>().Length);
            var paged = UnityEngine.Object.FindObjectOfType<FlipPageLoopScrollView>();
            paged.SetToPageIndex(3);
            Assert.AreEqual(3, paged.CurrentPage);
            GameObject.Find("NavSpine").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var spine = UnityEngine.Object.FindObjectOfType<SpinePlayer>();
            Assert.IsTrue(spine.HasAnimation("show"));
            GameObject.Find("PlaySequence").GetComponent<Button>().onClick.Invoke();
            var sequence = UnityEngine.Object.FindObjectOfType<SpineSequence>();
            yield return WaitFor(() => !sequence.IsRunning);
            Assert.IsNull(sequence.Error);
            GameObject.Find("NavEffects").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            var manager = Starter.Extensions.Effects.ParticleSimulationBudgetManager.Instance;
            Assert.IsNotNull(manager);
            Assert.AreEqual(3, manager.RegisteredCount);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var gallery = SceneManager.GetSceneByName("ExtensionGallery");
            if (!gallery.IsValid() || !gallery.isLoaded) yield break;
            var empty = SceneManager.CreateScene("AfterGallery");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(gallery);
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Gallery interaction timed out.");
                yield return null;
            }
        }
    }
}
