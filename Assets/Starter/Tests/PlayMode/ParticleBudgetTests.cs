using System.Collections;
using NUnit.Framework;
using Starter.Extensions.Effects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starter.Tests
{
    public sealed class ParticleBudgetTests
    {
        [UnityTest]
        public IEnumerator ParticleEmissionFadeHandlesNonParticleChildrenAndPausedTime()
        {
            var particleObject = new GameObject("fade", typeof(ParticleSystem));
            new GameObject("unrelated child").transform.SetParent(particleObject.transform);
            float previousScale = Time.timeScale;
            try
            {
                var type = typeof(ParticleSimulationBudgetManager).Assembly.GetType("Starter.Extensions.Effects.ParticleTransition");
                Assert.IsNotNull(type, "Migrate the particle emission fade helper.");
                var system = particleObject.GetComponent<ParticleSystem>();
                var main = system.main; main.startColor = new Color(1, 0, 0, 0.8f);
                var fade = (ParticleTransition)particleObject.AddComponent(type);
                Time.timeScale = 0;
                fade.VnishTransition(0.05f);
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.AreEqual(0, main.startColor.color.a, 0.0001f);
                fade.ShowTransition(0f);
                Assert.AreEqual(0.8f, main.startColor.color.a, 0.0001f);
            }
            finally { Time.timeScale = previousScale; Object.Destroy(particleObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControllerUnregistersOnDisableAndRestoresAutomaticSimulation()
        {
            var managerObject = new GameObject("manager", typeof(ParticleSimulationBudgetManager));
            var particleObject = new GameObject("particles", typeof(ParticleSystem));
            try
            {
                var manager = managerObject.GetComponent<ParticleSimulationBudgetManager>();
                var system = particleObject.GetComponent<ParticleSystem>();
                system.Play();
                var controller = particleObject.AddComponent<ParticleSystemController>();
                Assert.AreEqual(1, manager.RegisteredCount);
                Assert.IsTrue(system.isPaused);
                controller.enabled = false;
                Assert.AreEqual(0, manager.RegisteredCount);
                Assert.IsTrue(system.isPlaying);
                controller.enabled = true;
                Assert.AreEqual(1, manager.RegisteredCount);
                manager.enabled = false;
                Assert.AreEqual(0, manager.RegisteredCount);
                Assert.IsTrue(system.isPlaying);
                manager.enabled = true;
                yield return null;
                Assert.AreEqual(1, manager.RegisteredCount);
            }
            finally { Object.Destroy(particleObject); Object.Destroy(managerObject); }
            yield return null;
        }
    }
}
