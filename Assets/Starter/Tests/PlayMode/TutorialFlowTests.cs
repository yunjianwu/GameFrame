using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Starter.Extensions.Tutorial;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Starter.Tests
{
    public sealed class TutorialFlowTests
    {
        private GameObject root;
        private TutorialConfig config;
        private TutorialRunner runner;
        private TutorialTargetRegistry registry;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("tutorial test");
            registry = root.AddComponent<TutorialTargetRegistry>();
            runner = root.AddComponent<TutorialRunner>();
            config = ScriptableObject.CreateInstance<TutorialConfig>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(config);
            yield return null;
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator StopCancelsLateTargetAndReopenBindsOnlyOnce()
        {
            config.steps.Add(new TutorialStep { id = "click", type = TutorialStepType.Click, targetId = "late", fingerDelaySeconds = 0 });
            var store = new MemoryTutorialProgressStore();
            runner.Configure(config, registry, store);
            int completed = 0;
            runner.StepCompleted += _ => completed++;
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.WaitingForTarget);
            runner.Stop();
            var button = Target("late");
            yield return null;
            button.onClick.Invoke();
            Assert.AreEqual(TutorialState.Stopped, runner.State);
            Assert.AreEqual(0, completed);
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.Running);
            button.onClick.Invoke();
            yield return Until(() => runner.State == TutorialState.Completed);
            Assert.AreEqual(1, completed);
            Assert.IsTrue(store.TryLoad(config.tutorialId, out string saved));
            Assert.AreEqual(string.Empty, saved);
            button.onClick.Invoke();
            Assert.AreEqual(1, completed);
        }

        [UnityTest]
        public IEnumerator SignalsRequireCorrectEventAndResumeByStableId()
        {
            config.steps.Add(new TutorialStep { id = "first", type = TutorialStepType.Signal, completionSignal = "merged", fingerDelaySeconds = 0 });
            config.steps.Add(new TutorialStep { id = "second", type = TutorialStepType.Signal, completionSignal = "submitted", fingerDelaySeconds = 0 });
            var store = new MemoryTutorialProgressStore();
            runner.Configure(config, registry, store);
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.Running);
            runner.Notify("unrelated");
            yield return null;
            Assert.AreEqual("first", runner.CurrentStep.id);
            runner.Notify("merged");
            yield return Until(() => runner.CurrentStep != null && runner.CurrentStep.id == "second");
            runner.Stop();
            config.steps.Insert(0, new TutorialStep { id = "new step", type = TutorialStepType.Delay });
            runner.Begin();
            yield return Until(() => runner.State == TutorialState.Running);
            Assert.AreEqual("second", runner.CurrentStep.id);
            runner.Notify("submitted");
            yield return Until(() => runner.State == TutorialState.Completed);
        }

        [UnityTest]
        public IEnumerator SignalDuringPresentationCompletesOnlyTheCurrentStep()
        {
            config.steps.Add(new TutorialStep { id = "first", type = TutorialStepType.Signal, completionSignal = "done", fingerDelaySeconds = 0.1f });
            config.steps.Add(new TutorialStep { id = "second", type = TutorialStepType.Signal, completionSignal = "done", fingerDelaySeconds = 0 });
            runner.Configure(config, registry);
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.Presenting);
            runner.Notify("done");
            yield return Until(() => runner.CurrentStep != null && runner.CurrentStep.id == "second" && runner.State == TutorialState.Running);
            yield return null;
            Assert.AreEqual(TutorialState.Running, runner.State, "An earlier completion signal must not complete another step.");
            runner.Notify("done");
            yield return Until(() => runner.State == TutorialState.Completed);
        }

        [UnityTest]
        public IEnumerator MissingTargetTimesOutEvenWhenGameIsPaused()
        {
            config.steps.Add(new TutorialStep { id = "missing", type = TutorialStepType.Click, targetId = "missing", targetTimeoutSeconds = 0.05f });
            runner.Configure(config, registry);
            Time.timeScale = 0;
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.Failed);
            StringAssert.Contains("missing", runner.Error);
        }

        [UnityTest]
        public IEnumerator TargetDisableRebindsWithoutCompletingAndSkipIsExplicit()
        {
            config.steps.Add(new TutorialStep { id = "click", type = TutorialStepType.Click, targetId = "button", skippable = true, fingerDelaySeconds = 0 });
            var button = Target("button");
            runner.Configure(config, registry);
            runner.Begin(false);
            yield return Until(() => runner.State == TutorialState.Running);
            button.gameObject.SetActive(false);
            yield return Until(() => runner.State == TutorialState.WaitingForTarget);
            button.gameObject.SetActive(true);
            yield return Until(() => runner.State == TutorialState.Running);
            Assert.AreEqual("click", runner.CurrentStep.id);
            Assert.IsTrue(runner.SkipCurrent());
            yield return Until(() => runner.State == TutorialState.Completed);
        }

        private Button Target(string id)
        {
            var target = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(TutorialTarget));
            target.transform.SetParent(root.transform, false);
            target.GetComponent<TutorialTarget>().Configure(id, registry);
            return target.GetComponent<Button>();
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Tutorial did not reach the expected state.");
                yield return null;
            }
        }
    }
}
