using System;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Extensions.UI
{
    [RequireComponent(typeof(Text))]
    public sealed class CommonStoryText : BaseMeshEffect
    {
        [SerializeField, Min(0.001f)] private float interval = 0.04f;
        private Text textComponent;
        private Action completed;
        private float elapsed;
        private int glyphCount = -1;
        private int visible;
        public bool IsPlaying { get; private set; }

        protected override void Awake() { base.Awake(); textComponent = GetComponent<Text>(); }
        public void PlayAni(string content, Action onComplete = null)
        {
            if (textComponent == null) textComponent = GetComponent<Text>();
            completed = onComplete;
            elapsed = 0; visible = 0; glyphCount = -1; IsPlaying = true;
            textComponent.text = content ?? string.Empty;
            graphic.SetVerticesDirty();
            if (textComponent.text.Length == 0) Finish();
        }
        public void StopAni()
        {
            completed = null; IsPlaying = false;
            if (graphic != null) graphic.SetVerticesDirty();
        }
        public void ShowText() { if (IsPlaying) Finish(); }
        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            int count = Mathf.FloorToInt(elapsed / Mathf.Max(0.001f, interval));
            if (count != visible) { visible = count; graphic.SetVerticesDirty(); }
            if (glyphCount >= 0 && visible >= glyphCount) Finish();
        }
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !IsPlaying) return;
            glyphCount = vh.currentVertCount / 4;
            var vertex = UIVertex.simpleVert;
            for (int i = Mathf.Min(visible * 4, vh.currentVertCount); i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Color32 color = vertex.color; color.a = 0; vertex.color = color;
                vh.SetUIVertex(vertex, i);
            }
        }
        private void Finish()
        {
            IsPlaying = false;
            var callback = completed; completed = null;
            graphic.SetVerticesDirty();
            callback?.Invoke();
        }
        protected override void OnDisable() { StopAni(); base.OnDisable(); }
    }
}