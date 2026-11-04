using Starter.Extensions.Effects;
using Starter.Extensions.Spine;
using Starter.Extensions.Tutorial;
using Starter.Extensions.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Extensions.Samples
{
    /// <summary>Scene wiring only. Reusable components live in the four sibling modules.</summary>
    public sealed class ExtensionsDemo : MonoBehaviour
    {
        public GameObject[] pages;
        public Button[] navigation;
        public Text status;
        public TutorialRunner tutorial;
        public TutorialConfig tutorialConfig;
        public TutorialTargetRegistry targets;
        public TutorialMask mask;
        public FingerPointer finger;
        public TutorialInputGate gate;
        public GameObject dialogue;
        public Text dialogueText;
        public Button startGuide, resumeGuide, stopGuide, businessTarget;
        public UILoopScrollView virtualList;
        public FlipPageLoopScrollView pagedList;
        public ExpandLoopScrollView expandList;
        public ExpandTipsLoopScrollView tipsList;
        public Button previousPage, nextPage;
        public Text pageNumber;
        public CommonStoryText story;
        public Button replayStory;
        public RichText richText;
        public Sprite inlineSprite;
        public SpinePlayer spine;
        public SpineIntroLoop intro;
        public SpineTransition spineFade;
        public SpineSequence sequence;
        public Button playSequence, fadeSpine, speedSpine;
        public GameObject effectsWorld;
        public ParticleSimulationBudgetManager particleBudget;
        public Text particleStats;
        public Button toggleBudget;
        private readonly MemoryTutorialProgressStore progress = new MemoryTutorialProgressStore();
        private bool faded;
        private bool fast;
        private float nextStats;

        private void Start()
        {
            for (int i = 0; i < navigation.Length; i++)
            {
                int page = i;
                navigation[i].onClick.AddListener(() => ShowPage(page));
            }
            tutorial.Configure(tutorialConfig, targets, progress);
            tutorial.ConfigurePresentation(mask, finger, gate, dialogue, dialogueText);
            tutorial.StepEntered += id => status.text = "GUIDE / " + id.ToUpperInvariant();
            tutorial.Completed += () => status.text = "GUIDE COMPLETE / restart or resume to test saved progress";
            tutorial.Failed += error => status.text = error;
            startGuide.onClick.AddListener(() => StartTutorial(false));
            resumeGuide.onClick.AddListener(() => StartTutorial(true));
            stopGuide.onClick.AddListener(() => { tutorial.Stop(); status.text = "GUIDE STOPPED / subscriptions released"; });
            businessTarget.onClick.AddListener(() =>
            {
                if (gate.Allows("reward")) tutorial.Notify("reward.claimed");
            });
            virtualList.Init(BindCell, (_, index) => status.text = "VIRTUAL LIST / item " + index);
            virtualList.ShowList(1000);
            pagedList.InitPaged(BindCell, page => pageNumber.text = "PAGE " + page + " / 8");
            pagedList.ShowList(8);
            expandList.Init((header, cell, group, index) =>
            {
                header.GetComponentInChildren<Text>().text = "GROUP " + group + " / click to expand";
                if (cell != null) cell.GetComponentInChildren<Text>().text = "Group " + group + " / item " + index;
            }, (_, __, group, index) => status.text = "GROUP " + group + " / selected item " + index);
            expandList.ShowList("3|4|2");
            tipsList.Init(BindCell, (_, index) =>
            {
                tipsList.m_ExpandTips.GetComponentInChildren<Text>().text = "INLINE DETAIL / item " + index;
                status.text = "TIPS LIST / selected item " + index;
            });
            tipsList.ShowList(30);
            previousPage.onClick.AddListener(() => pagedList.SetToPageIndex(pagedList.CurrentPage - 1));
            nextPage.onClick.AddListener(() => pagedList.SetToPageIndex(pagedList.CurrentPage + 1));
            replayStory.onClick.AddListener(ReplayStory);
            richText.SpriteResolver = key => key == "coin" ? inlineSprite : null;
            richText.onHrefClick.AddListener(link => status.text = "RICH TEXT / link event: " + link);
            richText.RefreshSprites();
            playSequence.onClick.AddListener(() => { sequence.Play(); status.text = "SPINE / sequence running"; });
            sequence.Completed += () => status.text = "SPINE / sequence complete, idle loop active";
            sequence.Failed += error => status.text = error;
            fadeSpine.onClick.AddListener(() => { faded = !faded; spineFade.FadeTo(faded ? 0.15f : 1, 0.4f); });
            speedSpine.onClick.AddListener(() => { fast = !fast; spine.SetSpeed(fast ? 2 : 1); status.text = "SPINE / speed " + (fast ? "2x" : "1x"); });
            toggleBudget.onClick.AddListener(() => particleBudget.enabled = !particleBudget.enabled);
            ShowPage(0);
        }

        public void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Length) return;
            if (index != 0) tutorial.Stop();
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                navigation[i].GetComponent<Image>().color = i == index ? new Color32(205, 229, 133, 255) : new Color32(191, 204, 191, 255);
            }
            effectsWorld.SetActive(index == 4);
            if (index == 1) ReplayStory();
            status.text = "EXTENSIONS / " + navigation[index].GetComponentInChildren<Text>().text;
        }

        public void StartTutorial(bool resume)
        {
            ShowPage(0);
            tutorial.Begin(resume);
        }

        private static void BindCell(GameObject cell, int index) { cell.GetComponentInChildren<Text>().text = "ITEM " + index.ToString("0000"); }
        private void ReplayStory() { story.PlayAni("Typewriter text keeps rich-text markup and uses unscaled time. Reveal, cancel and reopen without duplicate callbacks.", () => status.text = "TEXT / typewriter complete"); }

        private void Update()
        {
            if (particleStats == null || !particleStats.gameObject.activeInHierarchy || Time.unscaledTime < nextStats) return;
            nextStats = Time.unscaledTime + 0.2f;
            particleStats.text = "REGISTERED  " + particleBudget.RegisteredCount + "\nNORMAL UPDATES  " + particleBudget.UpdatedNormalParticles +
                "\nCPU SIMULATION  " + particleBudget.LastSimulationMilliseconds.ToString("F3") + " ms\nBUDGET  " + (particleBudget.enabled ? "ON / soft 2.5 ms" : "OFF / Unity automatic");
        }

        private void OnDisable() { if (tutorial != null) tutorial.Stop(); }
    }
}
