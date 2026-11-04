using System.Reflection;
using NUnit.Framework;
using Starter.Extensions.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Starter.Tests
{
    public sealed class MigratedUIContracts
    {
        [Test]
        public void EmptyCircleFillProducesNoGeometry()
        {
            var go = new GameObject("circle", typeof(RectTransform), typeof(CircleImage));
            try
            {
                var image = go.GetComponent<CircleImage>();
                image.FillPercent = 0;
                using (var mesh = new VertexHelper())
                {
                    Populate(image, mesh);
                    Assert.AreEqual(0, mesh.currentVertCount);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PartialRoundedRectangleNeverReferencesMissingVertices()
        {
            var go = new GameObject("round", typeof(RectTransform), typeof(RoundRectangleImage));
            var mesh = new Mesh();
            try
            {
                var image = go.GetComponent<RoundRectangleImage>();
                image.fillPercent = 0;
                using (var vertices = new VertexHelper())
                {
                    Populate(image, vertices);
                    Assert.AreEqual(0, vertices.currentVertCount);
                    vertices.FillMesh(mesh);
                }
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void RoundedRadiusKeepsGeometryInsideRect()
        {
            var go = new GameObject("round", typeof(RectTransform), typeof(RoundRectangleImage));
            try
            {
                var image = go.GetComponent<RoundRectangleImage>(); image.rectTransform.sizeDelta = new Vector2(120, 60);
                image.roundRectangleRadius = 999;
                using (var vertices = new VertexHelper())
                {
                    Populate(image, vertices);
                    var vertex = new UIVertex();
                    for (int i = 0; i < vertices.currentVertCount; i++)
                    {
                        vertices.PopulateUIVertex(ref vertex, i);
                        Assert.LessOrEqual(Mathf.Abs(vertex.position.x), 60.001f);
                        Assert.LessOrEqual(Mathf.Abs(vertex.position.y), 30.001f);
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DisabledRadioButtonIgnoresClick()
        {
            var eventSystemObject = new GameObject("events", typeof(EventSystem));
            var go = new GameObject("radio", typeof(RectTransform), typeof(RadioButton));
            try
            {
                var radio = go.GetComponent<RadioButton>();
                radio.interactable = false;
                radio.OnPointerClick(new PointerEventData(eventSystemObject.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left });
                Assert.IsFalse(radio.IsTrue);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(eventSystemObject); }
        }

        [Test]
        public void RichTextResolvesInlineImagesWithoutGameResourceService()
        {
            var go = new GameObject("rich", typeof(RectTransform), typeof(RichText));
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                var rich = go.GetComponent<RichText>();
                rich.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                rich.SpriteResolver = key => key == "coin" ? sprite : null;
                rich.text = "Reward <#coin>";
                rich.RefreshSprites();
                var image = rich.GetComponentInChildren<Image>(true);
                Assert.IsNotNull(image);
                Assert.AreSame(sprite, image.sprite);
                Assert.IsFalse(image.raycastTarget);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        private static void Populate(Graphic graphic, VertexHelper mesh)
        {
            graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
        }
    }
}
