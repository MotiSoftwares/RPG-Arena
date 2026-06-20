using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using RPGArena.Core;
using RPGArena.Combat;

namespace RPGArena.Narrative
{
    // Wraps an Ink Story and a simple dialogue UI (CLAUDE.md §13). It is the DEEP hook between
    // narrative and combat: the intro's choice both sets an Ink variable AND calls an EXTERNAL
    // function the game binds here, so the player's choice changes how the fight opens
    // (taunt => the Dragon opens by charging; study => its weakness is revealed in the HUD). The
    // outro reads variables the runner sets from the real battle result, so it references what
    // actually happened. The BattleController waits on IsIntroDone before the first round.
    public class NarrativeRunner : MonoBehaviour, IBattleIntro
    {
        [Header("Compiled Ink stories (.json TextAssets)")]
        public TextAsset introJson;
        public TextAsset outroJson;

        // Read by the BattleController after the intro finishes.
        public bool IsIntroDone { get; private set; }
        public bool StartTelegraph { get; private set; }   // taunt => Dragon opens charging
        public bool RevealWeak { get; private set; }        // study => reveal weakness in HUD

        private Story story;
        private Font font;
        private Canvas canvas;
        private GameObject panel;
        private Text storyText;
        private RectTransform choiceBox;
        private readonly List<GameObject> choiceButtons = new();

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
        }

        private void Start()
        {
            // The intro is the Dragon's pre-fight scene (the first boss of a run). Later bosses in
            // the gauntlet skip it so the fight starts immediately.
            var run = GameBootstrap.Instance != null ? GameBootstrap.Instance.Run : null;
            bool firstBoss = run == null || run.CurrentBoss == "Dragon";
            if (introJson != null && firstBoss) PlayIntro();
            else IsIntroDone = true;       // no story (or not the first boss) => don't block the fight
        }

        // --- intro --------------------------------------------------------------------
        private void PlayIntro()
        {
            story = new Story(introJson.text);
            // Bind the EXTERNALs the .ink calls. lookaheadSafe:true keeps Ink from erroring if it
            // evaluates the line during its glue/look-ahead pass.
            story.BindExternalFunction("StartWithTelegraph", () => { StartTelegraph = true; }, true);
            story.BindExternalFunction("RevealWeakness", () => { RevealWeak = true; }, true);
            panel.SetActive(true);
            Advance();
        }

        // --- outro (called by the BattleController with the real result) --------------
        public void PlayOutro(bool won, bool brokeBoss, int heroesLost)
        {
            if (outroJson == null) return;
            story = new Story(outroJson.text);
            story.variablesState["won"] = won;
            story.variablesState["broke_boss"] = brokeBoss;
            story.variablesState["heroes_lost"] = heroesLost;
            panel.SetActive(true);
            Advance();
        }

        // --- Ink loop -----------------------------------------------------------------
        private void Advance()
        {
            var sb = new StringBuilder();
            while (story.canContinue) sb.AppendLine(story.Continue().Trim());
            storyText.text = sb.ToString().Trim();

            ClearChoices();
            if (story.currentChoices.Count > 0)
            {
                for (int i = 0; i < story.currentChoices.Count; i++)
                {
                    int idx = i;
                    MakeChoice(story.currentChoices[i].text, () => { story.ChooseChoiceIndex(idx); Advance(); });
                }
            }
            else
            {
                // End of this story beat — a dismiss button hands control back.
                MakeChoice("Continue  ▶", Dismiss);
            }
        }

        private void Dismiss()
        {
            panel.SetActive(false);
            IsIntroDone = true;        // unblocks the fight (no-op for the outro)
        }

        // --- UI -----------------------------------------------------------------------
        private void BuildUI()
        {
            var go = new GameObject("NarrativeCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;                 // above HUD + juice so the dialogue reads on top
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;

            panel = NewPanel(root, new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1500, 360), new Color(0.03f, 0.03f, 0.06f, 0.94f));
            var prt = (RectTransform)panel.transform;
            storyText = NewText(prt, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(1440, 200), 24, TextAnchor.UpperLeft);
            var cb = new GameObject("Choices"); cb.transform.SetParent(prt, false);
            choiceBox = cb.AddComponent<RectTransform>();
            choiceBox.anchorMin = new Vector2(0.5f, 0f); choiceBox.anchorMax = new Vector2(0.5f, 0f); choiceBox.pivot = new Vector2(0.5f, 0f);
            choiceBox.anchoredPosition = new Vector2(0, 16); choiceBox.sizeDelta = new Vector2(1440, 150);

            panel.SetActive(false);
        }

        private void MakeChoice(string label, UnityEngine.Events.UnityAction onClick)
        {
            float y = choiceButtons.Count * 40f;
            var go = new GameObject("Choice"); go.transform.SetParent(choiceBox, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.3f, 0.5f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(1200, 36);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
            var t = NewText(rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 34), 20, TextAnchor.MiddleCenter);
            t.text = label;
            choiceButtons.Add(go);
        }

        private void ClearChoices()
        {
            foreach (var b in choiceButtons) Destroy(b);
            choiceButtons.Clear();
        }

        private GameObject NewPanel(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        private Text NewText(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
