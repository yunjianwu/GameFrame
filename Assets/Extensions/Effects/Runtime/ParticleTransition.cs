using System;
using System.Collections;
using UnityEngine;

namespace Starter.Extensions.Effects
{
    /// <summary>Fades emission start colors. Particles already alive keep their own colors.</summary>
    public sealed class ParticleTransition : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem.MinMaxGradient[] original;
        private Coroutine routine;
        private float alpha = 1;

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            original = new ParticleSystem.MinMaxGradient[systems.Length];
            for (int i = 0; i < systems.Length; i++) original[i] = systems[i].main.startColor;
        }

        public void VnishTransition(float seconds) { FadeTo(0, seconds); }
        public void ShowTransition(float seconds) { FadeTo(1, seconds); }
        public void FadeTo(float value, float seconds, Action completed = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (systems == null) Awake();
            value = Mathf.Clamp01(value);
            if (seconds <= 0 || !isActiveAndEnabled) { Apply(value); completed?.Invoke(); return; }
            routine = StartCoroutine(Fade(value, seconds, completed));
        }

        private IEnumerator Fade(float target, float seconds, Action completed)
        {
            float start = alpha, elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Apply(Mathf.Lerp(start, target, elapsed / seconds));
                yield return null;
            }
            Apply(target); routine = null; completed?.Invoke();
        }

        private void Apply(float value)
        {
            alpha = value;
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                var main = systems[i].main;
                if (value == 1) { main.startColor = original[i]; continue; }
                var gradient = original[i];
                switch (gradient.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        gradient.color = Scale(gradient.color, value); break;
                    case ParticleSystemGradientMode.TwoColors:
                        gradient.colorMin = Scale(gradient.colorMin, value);
                        gradient.colorMax = Scale(gradient.colorMax, value); break;
                    case ParticleSystemGradientMode.Gradient:
                    case ParticleSystemGradientMode.RandomColor:
                        gradient.gradient = Scale(gradient.gradient, value); break;
                    case ParticleSystemGradientMode.TwoGradients:
                        gradient.gradientMin = Scale(gradient.gradientMin, value);
                        gradient.gradientMax = Scale(gradient.gradientMax, value); break;
                }
                main.startColor = gradient;
            }
        }

        private static Color Scale(Color color, float value) { color.a *= value; return color; }
        private static Gradient Scale(Gradient source, float value)
        {
            var keys = source.alphaKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].alpha *= value;
            var result = new Gradient { mode = source.mode };
            result.SetKeys(source.colorKeys, keys);
            return result;
        }

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (systems != null) Apply(1);
        }
    }
}
