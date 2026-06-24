using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPGArena.Core;
using RPGArena.Combat;

namespace RPGArena.UI
{
    // Owns the post-battle flow of the boss-rush run (CLAUDE.md §7 / Appendix E.2). On a win it
    // offers a choice of three boons and advances to the next boss (full HP/MP restore happens at
    // the next fight's setup); after the final boss it shows "run complete". On a loss it offers a
    // retry of the same boss. Pure presentation: it reads RunState + reloads the battle scene.
    public class RunFlow : MonoBehaviour
    {
        [Header("Channels (subscribed)")]
        public Core.Events.VoidChannel onBattleWon, onBattleLost;

        [Header("Content")]
        public List<BoonDefinition> boonRoster = new();

        [SerializeField] private TMP_FontAsset uiFont;   // SlimUI Poppins-Bold SDF (wired in scene); falls back to TMP default
        private TMP_FontAsset font;
        private Canvas canvas;
        private RectTransform root;
        private GameObject overlay;
        private BattleController controller;

        private void Awake()
        {
            font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            controller = FindFirstObjectByType<BattleController>();
            EnsureEventSystem();
            BuildCanvas();
        }

        // A turns + deaths based grade for the fight just won (§9.6 / §2.2 scoring).
        private string Grade()
        {
            int rounds = controller != null ? controller.RoundsTaken : 99;
            int lost = controller != null && controller.Context != null
                ? controller.Context.heroes.FindAll(h => !h.IsAlive).Count : 0;
            if (lost == 0 && rounds <= 8) return "S";
            if (lost == 0 && rounds <= 12) return "A";
            if (lost <= 1 && rounds <= 16) return "B";
            return "C";
        }

        private static Color GradeColor(string g) =>
            g == "S" ? new Color(1f, 0.85f, 0.2f) : g == "A" ? new Color(0.5f, 1f, 0.5f) :
            g == "B" ? new Color(0.6f, 0.8f, 1f) : new Color(0.85f, 0.6f, 0.5f);

        private void OnEnable()
        {
            onBattleWon?.Subscribe(OnWin);
            onBattleLost?.Subscribe(OnLose);
        }

        private void OnDisable()
        {
            onBattleWon?.Unsubscribe(OnWin);
            onBattleLost?.Unsubscribe(OnLose);
        }

        // --- outcomes -----------------------------------------------------------------
        private void OnWin(bool _)
        {
            var run = GameBootstrap.Instance != null ? GameBootstrap.Instance.Run : null;
            if (run != null && run.HasNextBoss) ShowBoonSelect(run);
            else ShowRunComplete(run);
        }

        private void OnLose(bool _) => ShowDefeat();

        // --- boon select (between bosses) ---------------------------------------------
        private void ShowBoonSelect(RunState run)
        {
            var panel = NewOverlay();
            var g = Grade();
            Label(panel, "VICTORY!", 0.9f, 46, new Color(1f, 0.9f, 0.4f));
            Label(panel, $"Battle Grade:  {g}", 0.83f, 30, GradeColor(g));
            Label(panel, $"Choose a boon  —  Next: {Pretty(run.NextBoss)}", 0.77f, 24, Color.white);
            Label(panel, RandomTip(), 0.72f, 18, new Color(0.75f, 0.85f, 1f));   // teach on the WIN screen too, not only on defeat

            var picks = PickThree();
            for (int i = 0; i < picks.Count; i++)
            {
                var boon = picks[i];
                float x = picks.Count == 1 ? 0.5f : 0.22f + i * (0.56f / Mathf.Max(1, picks.Count - 1));
                BoonCard(panel, boon, x, () =>
                {
                    run.acquiredBoons.Add(boon.name);
                    run.AdvanceBoss();
                    Reload();
                });
            }
        }

        // Pick up to three distinct random boons from the pool.
        private List<BoonDefinition> PickThree()
        {
            var pool = new List<BoonDefinition>();
            foreach (var b in boonRoster) if (b != null) pool.Add(b);
            // Fisher–Yates partial shuffle.
            for (int i = 0; i < pool.Count; i++)
            {
                int j = Random.Range(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (pool.Count > 3) pool.RemoveRange(3, pool.Count - 3);
            return pool;
        }

        private void ShowRunComplete(RunState run)
        {
            var panel = NewOverlay();
            var g = Grade();
            Label(panel, "THE ARENA IS CONQUERED", 0.64f, 52, new Color(1f, 0.85f, 0.3f));
            Label(panel, $"Final Battle Grade:  {g}", 0.55f, 32, GradeColor(g));
            Label(panel, "You have bested every champion of the Arena of the Algorithms.", 0.47f, 24, Color.white);
            MakeBtn(panel,"Return to Main Menu", 0.34f, () => { ResetRun(); ToMenu(); });
        }

        private void ShowDefeat()
        {
            var panel = NewOverlay();
            Label(panel, "DEFEAT", 0.64f, 56, new Color(0.9f, 0.3f, 0.3f));
            Label(panel, "The party has fallen. Try again?", 0.55f, 26, Color.white);
            Label(panel, RandomTip(), 0.46f, 19, new Color(0.75f, 0.85f, 1f));
            MakeBtn(panel,"Retry", 0.36f, Reload);
            MakeBtn(panel,"Return to Main Menu", 0.27f, () => { ResetRun(); ToMenu(); });
        }

        // A rotating gameplay tip on the defeat screen (layered onboarding, §13.4 / E.4).
        private static readonly string[] Tips =
        {
            "Tip: Exploit the boss's WEAKNESS (Ice on the Dragon) — and never use the element it ABSORBS (Fire heals it!).",
            "Tip: The dragon's SEARING FURY grows every turn it goes un-Broken — BREAK it to vent the rage, or be overwhelmed.",
            "Tip: Combo across classes — Wet (Thief) then Ice (Mage) to FREEZE, then a physical hit to SHATTER for huge damage.",
            "Tip: Mark the boss (Thief/Archer) then Freeze it — a Marked + Frozen target is BRITTLE and crits hard.",
            "Tip: Spamming your basic attack can't win — you'll die first. Combo, Break, and charge Valor for an OVERDRIVE surge.",
        };
        private int tipIndex;
        private string RandomTip() => Tips[(tipIndex++) % Tips.Length];

        // --- navigation ---------------------------------------------------------------
        private void Reload()
        {
            // Make sure time is running (juice/cheats may have changed it) before the reload.
            Time.timeScale = 1f;
            var boot = GameBootstrap.Instance;
            if (boot != null) boot.Scenes.LoadScene("BattleArena");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("BattleArena");
        }

        private void ToMenu()
        {
            Time.timeScale = 1f;
            var boot = GameBootstrap.Instance;
            if (boot != null) boot.Scenes.LoadScene("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void ResetRun()
        {
            var run = GameBootstrap.Instance != null ? GameBootstrap.Instance.Run : null;
            if (run != null) run.currentBossIndex = 0;   // party + boons reset on the next Character Select
        }

        private static string Pretty(string id) => id == "BlackMage" ? "The Black Mage" : id == "EvilWarrior" ? "The Evil Warrior" : "The " + id;

        // --- UI construction ----------------------------------------------------------
        private GameObject NewOverlay()
        {
            if (overlay != null) Destroy(overlay);
            overlay = new GameObject("Overlay");
            overlay.transform.SetParent(root, false);
            var img = overlay.AddComponent<Image>(); img.color = new Color(0.03f, 0.03f, 0.06f, 0.92f);
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return overlay;
        }

        private void BoonCard(GameObject parent, BoonDefinition boon, float anchorX, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("BoonCard"); go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.15f, 0.22f, 0.34f, 0.97f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.42f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 300);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { GameBootstrap.Instance?.Audio?.PlaySfx("ui_click"); onClick(); });
            var name = Text(rt, boon.displayName, new Vector2(0.5f, 0.86f), new Vector2(330, 50), 26, TextAnchor.MiddleCenter); name.color = new Color(1f, 0.9f, 0.5f);
            Text(rt, boon.description, new Vector2(0.5f, 0.42f), new Vector2(330, 200), 19, TextAnchor.UpperCenter);
        }

        private void Label(GameObject parent, string text, float anchorY, int size, Color color)
        {
            var t = Text((RectTransform)parent.transform, text, new Vector2(0.5f, anchorY), new Vector2(1500, size * 3 + 20), size, TextAnchor.MiddleCenter);
            t.color = color;
        }

        private Button MakeBtn(GameObject parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.3f, 0.5f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 54);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { GameBootstrap.Instance?.Audio?.PlaySfx("ui_click"); onClick(); });
            Text(rt, label, new Vector2(0.5f, 0.5f), new Vector2(360, 54), 22, TextAnchor.MiddleCenter);
            return btn;
        }

        private static TextAlignmentOptions MapAlign(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            _ => TextAlignmentOptions.Center
        };

        private TMP_Text Text(RectTransform parent, string content, Vector2 anchor, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = content; t.fontSize = fontSize; t.alignment = MapAlign(align); t.color = Color.white;
            t.richText = true; t.raycastTarget = false; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
            return t;
        }

        private void BuildCanvas()
        {
            var go = new GameObject("RunFlowCanvas"); go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
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
