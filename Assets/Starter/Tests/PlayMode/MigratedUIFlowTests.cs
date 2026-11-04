using System.Collections;
using NUnit.Framework;
using Starter.Extensions.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Starter.Tests
{
    public sealed class MigratedUIFlowTests
    {
        private GameObject root;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ui contracts", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator ListRecyclesCellsAndRepeatedInitDoesNotDuplicateClicks()
        {
            var listObject = new GameObject("list", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(UILoopScrollView));
            listObject.transform.SetParent(root.transform, false);
            listObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 180);
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(listObject.transform, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(300, 180);
            var cell = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(content, false);
            cell.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 50);
            var scroll = listObject.GetComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = listObject.GetComponent<RectTransform>();
            scroll.horizontal = false;
            var list = listObject.GetComponent<UILoopScrollView>();
            list.m_Direction = Scroll_Direction.Vertical; list.m_IsShowArrow = false; list.m_CellGameObject = cell;
            int clicks = 0;
            Canvas.ForceUpdateCanvases();
            list.Init((_, __) => { }, (_, __) => clicks++);
            list.ShowList(100);
            yield return null;
            Assert.Less(content.childCount, 10, "Only visible cells should be instantiated.");
            content.anchoredPosition = new Vector2(0, 4800);
            scroll.onValueChanged.Invoke(Vector2.zero);
            yield return null;
            list.Init((_, __) => { }, (_, __) => clicks++);
            list.ShowList(100);
            list.ToItem(0);
            scroll.onValueChanged.Invoke(Vector2.one);
            foreach (var button in content.GetComponentsInChildren<Button>())
            {
                button.onClick.Invoke();
                break;
            }
            Assert.AreEqual(1, clicks);
            Assert.Less(content.childCount, 16, "Scrolling and reopening must reuse the cell pool.");
        }

        [UnityTest]
        public IEnumerator TabTracksProgrammaticSelectionWithStandardToggleGroup()
        {
            var page = new GameObject("page"); page.transform.SetParent(root.transform);
            var tabObject = new GameObject("tab", typeof(RectTransform), typeof(Tab));
            tabObject.transform.SetParent(root.transform);
            var tab = tabObject.GetComponent<Tab>();
            tab.TargetPage = page;
            var group = root.AddComponent<ToggleGroup>(); group.allowSwitchOff = true; tab.group = group;
            yield return null;
            tab.isOn = false;
            Assert.IsFalse(page.activeSelf);
            tab.isOn = true;
            Assert.IsTrue(page.activeSelf);
            tabObject.SetActive(false); tabObject.SetActive(true);
            tab.isOn = false;
            Assert.IsFalse(page.activeSelf);
        }

        [UnityTest]
        public IEnumerator ExpandListReinitializesWithoutDuplicateHeadersOrCellClicks()
        {
            var listObject = new GameObject("expand", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(ExpandLoopScrollView));
            listObject.transform.SetParent(root.transform, false);
            listObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 260);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(listObject.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(300, 260);
            var cell = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(content.transform, false);
            cell.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 40);
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(Button));
            header.transform.SetParent(content.transform, false);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 40);
            var scroll = listObject.GetComponent<ScrollRect>();
            scroll.content = contentRect; scroll.viewport = listObject.GetComponent<RectTransform>(); scroll.horizontal = false;
            var list = listObject.GetComponent<ExpandLoopScrollView>();
            list.m_Direction = Scroll_Direction.Vertical; list.m_CellGameObject = cell;
            list.m_ExpandButton = header; list.m_IsExpand = true; list.m_IsShowArrow = false;
            int clicks = 0;
            System.Action<GameObject, GameObject, int, int> bind = (_, __, ___, ____) => { };
            System.Action<GameObject, GameObject, int, int> click = (_, __, ___, ____) => clicks++;
            list.Init(bind, click); list.ShowList("2|2");
            list.Init(bind, click); list.ShowList("2|2");
            yield return null;
            foreach (var button in content.GetComponentsInChildren<Button>())
                if (button.name.Contains("-")) { button.onClick.Invoke(); break; }
            Assert.AreEqual(1, clicks);
            var activeHeaders = new System.Collections.Generic.HashSet<string>();
            foreach (var button in content.GetComponentsInChildren<Button>())
                if (!button.name.Contains("-")) Assert.IsTrue(activeHeaders.Add(button.name), "Each header needs a distinct pooled object.");
            Assert.AreEqual(2, activeHeaders.Count);
        }

        [UnityTest]
        public IEnumerator VariableSizeListSnapsAfterDragWithoutIndexingBeforeTheFirstItem()
        {
            var list = CreateList<UILoopScrollView>(new Vector2(300, 180), new Vector2(280, 50));
            list.m_endDragItemToTop = true;
            list.Init((_, __) => { });
            list.ShowList(5, new[] { new Vector2(280, 50), new Vector2(280, 90), new Vector2(280, 60), new Vector2(280, 60), new Vector2(280, 60) });
            var content = list.GetComponent<ScrollRect>().content;
            content.anchoredPosition = new Vector2(0, 40);
            Assert.DoesNotThrow(() => list.OnEndDrag(new PointerEventData(null)));
            yield return UntilPosition(content, new Vector2(0, 50));
        }

        [Test]
        public void EmptyListIgnoresDragSnapping()
        {
            var list = CreateList<UILoopScrollView>(new Vector2(300, 180), new Vector2(280, 50));
            list.m_endDragItemToTop = true;
            list.Init((_, __) => { });
            list.ShowList(0);
            Assert.DoesNotThrow(() => list.OnEndDrag(new PointerEventData(null)));
        }

        [UnityTest]
        public IEnumerator HorizontalListSnapsToANegativeContentOffset()
        {
            var list = CreateList<UILoopScrollView>(new Vector2(180, 100), new Vector2(50, 80), Scroll_Direction.Horizontal);
            list.m_endDragItemToTop = true;
            list.Init((_, __) => { });
            list.ShowList(8);
            var content = list.GetComponent<ScrollRect>().content;
            content.anchoredPosition = new Vector2(-40, 0);
            list.OnEndDrag(new PointerEventData(null));
            yield return UntilPosition(content, new Vector2(-50, 0));
        }

        [Test]
        public void InlineTipsRebindRecycledItemsWhenDetailsClose()
        {
            var list = CreateList<ExpandTipsLoopScrollView>(new Vector2(300, 180), new Vector2(280, 50));
            var content = list.GetComponent<ScrollRect>().content;
            var tips = new GameObject("Details", typeof(RectTransform));
            tips.transform.SetParent(content, false);
            tips.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 120);
            list.m_ExpandTips = tips;
            var clicked = new System.Collections.Generic.List<int>();
            list.Init((cell, index) => cell.GetComponentInChildren<Text>().text = index.ToString(), (_, index) => clicked.Add(index));
            list.ShowList(10);
            var first = content.Find("0").GetComponent<Button>();
            first.onClick.Invoke();
            first.onClick.Invoke();
            Button third = null;
            foreach (var button in content.GetComponentsInChildren<Button>())
                if (Mathf.Approximately(button.GetComponent<RectTransform>().anchoredPosition.y, -100)) third = button;
            Assert.IsNotNull(third);
            Assert.AreEqual("3", third.GetComponentInChildren<Text>().text, "Recycled cells need fresh content for their new index.");
            third.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, clicked, "Each click must report its current one-based data index once.");
        }

        [Test]
        public void GroupedListRecomputesScrollRangeWhenDataChanges()
        {
            var list = CreateList<ExpandLoopScrollView>(new Vector2(300, 260), new Vector2(280, 40));
            var content = list.GetComponent<ScrollRect>().content;
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(Button));
            header.transform.SetParent(content, false);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 40);
            list.m_ExpandButton = header; list.m_IsExpand = true;
            list.Init((_, __, ___, ____) => { });
            list.ShowList("1");
            Assert.AreEqual(80, content.sizeDelta.y);
            list.ShowList("20|20");
            Assert.AreEqual(1680, content.sizeDelta.y, "New groups and items must extend the scroll range.");
            list.OnClickExpand(1);
            list.ShowList("2|3");
            Assert.AreEqual(200, content.sizeDelta.y, "Refresh must retain the first group's collapsed state.");
            list.ShowList("0");
            Assert.AreEqual(40, content.sizeDelta.y, "Shrinking data must remove obsolete scroll space.");
        }

        private T CreateList<T>(Vector2 viewportSize, Vector2 cellSize, Scroll_Direction direction = Scroll_Direction.Vertical) where T : UILoopScrollView
        {
            var listObject = new GameObject("list", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(T));
            listObject.transform.SetParent(root.transform, false);
            var viewport = listObject.GetComponent<RectTransform>();
            viewport.sizeDelta = viewportSize;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.sizeDelta = viewportSize;
            var cell = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(content, false);
            cell.GetComponent<RectTransform>().sizeDelta = cellSize;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(cell.transform, false);
            label.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scroll = listObject.GetComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = direction == Scroll_Direction.Horizontal; scroll.vertical = !scroll.horizontal;
            scroll.inertia = false;
            var list = listObject.GetComponent<T>();
            list.m_Direction = direction; list.m_IsShowArrow = false; list.m_CellGameObject = cell;
            Canvas.ForceUpdateCanvases();
            return list;
        }

        private static IEnumerator UntilPosition(RectTransform content, Vector2 expected)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (Vector2.Distance(content.anchoredPosition, expected) > 0.1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(Vector2.Distance(content.anchoredPosition, expected), 0.1f, "Content must snap to the nearest item's leading edge.");
        }

        [UnityTest]
        public IEnumerator ButtonScaleRestoresOriginalNonUniformScaleOnDisable()
        {
            var button = new GameObject("button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(root.transform);
            button.transform.localScale = new Vector3(2, 0.5f, 1);
            var effect = button.AddComponent<UIBtnScaleEffect>();
            effect.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            yield return null;
            button.SetActive(false);
            Assert.AreEqual(new Vector3(2, 0.5f, 1), button.transform.localScale);
        }

        [UnityTest]
        public IEnumerator TypewriterImmediateRevealCompletesOnceAndCanRestart()
        {
            var label = new GameObject("text", typeof(RectTransform), typeof(Text), typeof(CommonStoryText));
            label.transform.SetParent(root.transform);
            label.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var story = label.GetComponent<CommonStoryText>();
            int complete = 0;
            story.PlayAni("First story", () => complete++);
            story.ShowText(); story.ShowText();
            Assert.AreEqual(1, complete);
            story.PlayAni("Second story", () => complete++);
            story.StopAni();
            yield return null;
            Assert.AreEqual(1, complete);
            Assert.AreEqual("Second story", label.GetComponent<Text>().text);
        }
    }
}
