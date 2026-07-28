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
        public List<ItemDefinition> itemCatalog = new();   // Supply Camp stock (wired in scene)

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
        private int lastReward;

        // Gold payout scales with the battle grade — clean fast wins fund a richer Supply Camp.
        private int GradeGold(string g) => g == "S" ? 260 : g == "A" ? 210 : g == "B" ? 160 : 120;

        // A clean fast win seeds the next fight's Valor, so the grade compounds into the run instead
        // of being a letter you click past. S is a third of an Overdrive already banked.
        private static float GradeValor(string g) => g == "S" ? 30f : g == "A" ? 20f : g == "B" ? 10f : 0f;

        private void OnWin(bool _)
        {
            var run = GameBootstrap.Instance != null ? GameBootstrap.Instance.Run : null;
            if (run != null)
            {
                var g = Grade();
                lastReward = GradeGold(g);
                run.gold += lastReward;
                run.startValor = GradeValor(g);
                RecordAttrition(run);
            }
            if (run != null && run.HasNextBoss) ShowBoonSelect(run);
            else ShowRunComplete(run);
        }

        // Carry each survivor's remaining HP/MP into the next fight. A hero who FELL is recorded at
        // zero and clamped up to the floor at setup — they are back on their feet for the next boss,
        // but at the worst legal state, so losing someone costs you something beyond the round it
        // happened in.
        private void RecordAttrition(RunState run)
        {
            if (controller == null || controller.Context == null) return;
            foreach (var h in controller.Context.heroes)
            {
                if (h == null || h.stats == null) continue;
                float hp = h.stats.maxHP > 0 ? (float)h.currentHP / h.stats.maxHP : 1f;
                float mp = h.stats.maxMP > 0 ? (float)h.currentMP / h.stats.maxMP : 1f;
                run.SetCarry(h.displayName, h.IsAlive ? hp : 0f, mp);
            }
        }

        private void OnLose(bool _) => ShowDefeat();

        // --- boon select (between bosses) ---------------------------------------------
        private void ShowBoonSelect(RunState run)
        {
            var panel = NewOverlay();
            var g = Grade();
            Label(panel, "VICTORY!", 0.9f, 46, new Color(1f, 0.9f, 0.4f));
            Label(panel, $"Battle Grade:  {g}      <color=#F2C14E>+{lastReward} gold</color>  ({run.gold} total)", 0.83f, 30, GradeColor(g));
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
                    ShowShop(run);           // spend the spoils before marching on
                });
            }
        }

        // --- Supply Camp (the between-bosses shop) ------------------------------------
        private TMP_Text shopGold;
        private readonly List<System.Action> shopRefreshers = new();

        private TMP_Text partyCondition;

        private void ShowShop(RunState run)
        {
            var panel = NewOverlay();
            shopRefreshers.Clear();
            Label(panel, "SUPPLY CAMP", 0.94f, 44, new Color(1f, 0.9f, 0.4f));
            shopGold = Text((RectTransform)panel.transform, "", new Vector2(0.5f, 0.88f), new Vector2(900, 40), 28, TextAnchor.MiddleCenter);
            shopGold.color = new Color(0.98f, 0.80f, 0.32f);

            // The party's condition is the whole reason this screen is a decision now — the player
            // has to see the wounds to weigh healing them against buying for the next fight.
            partyCondition = Text((RectTransform)panel.transform, "", new Vector2(0.5f, 0.82f), new Vector2(1500, 34), 22, TextAnchor.MiddleCenter);
            Label(panel, "Wounds carry to the next boss. Rest to mend them — or spend it all on the fight ahead.", 0.765f, 19, new Color(0.75f, 0.85f, 1f));

            int shown = 0;
            foreach (var item in itemCatalog)
            {
                if (item == null || item.ability == null) continue;
                float x = 0.18f + (shown % 4) * 0.213f;
                float yRow = shown < 4 ? 0.56f : 0.32f;
                ItemCard(panel, run, item, x, yRow);
                shown++;
                if (shown >= 8) break;
            }

            var rest = MakeBtnAt(panel, "", 0.28f, 0.12f, () =>
            {
                if (run.gold < run.RestCost) return;
                run.gold -= run.RestCost;
                run.restsPurchased++;
                run.RestParty(RestFraction);
                RefreshShop(run);
            });
            var restLabel = rest.GetComponentInChildren<TMP_Text>();
            shopRefreshers.Add(() =>
            {
                bool can = run.gold >= run.RestCost;
                restLabel.text = $"REST  —  {run.RestCost}g";
                restLabel.color = can ? Color.white : new Color(1f, 0.55f, 0.5f);
                ((Image)rest.targetGraphic).color = can ? new Color(0.18f, 0.34f, 0.42f, 0.95f) : new Color(0.2f, 0.22f, 0.26f, 0.9f);
                rest.interactable = can;
            });

            MakeBtnAt(panel, $"March on  —  Next: {Pretty(run.NextBoss)}", 0.7f, 0.12f, () => { run.AdvanceBoss(); Reload(); });
            RefreshShop(run);
        }

        // One rest brings the whole party up to this fraction of max (it never downgrades anyone who
        // is already healthier). Below 1.0 on purpose: even a rested party carries something forward,
        // so a sloppy win is still felt in the next fight.
        private const float RestFraction = 0.85f;

        private void RefreshShop(RunState run)
        {
            if (shopGold != null) shopGold.text = $"GOLD:  {run.gold}";
            if (partyCondition != null) partyCondition.text = ConditionLine(run);
            foreach (var r in shopRefreshers) r?.Invoke();
        }

        // "Warrior 62%   Mage 50%   Thief 88%" — colour-coded, using the same floors the next fight
        // will actually clamp to, so the number shown is the number the player gets.
        private string ConditionLine(RunState run)
        {
            var sb = new System.Text.StringBuilder("PARTY:   ");
            foreach (var kv in run.carryHp)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp(kv.Value, RunState.CarryHpFloor, 1f) * 100f);
                string hex = pct >= 85 ? "8FE38F" : pct >= 65 ? "E3D98F" : "E38F8F";
                sb.Append($"<color=#{hex}>{kv.Key} {pct}%</color>    ");
            }
            return run.carryHp.Count == 0 ? "" : sb.ToString();
        }

        private void ItemCard(GameObject parent, RunState run, ItemDefinition item, float anchorX, float anchorY)
        {
            var go = new GameObject("ItemCard"); go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.15f, 0.20f, 0.32f, 0.97f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(340, 210);

            var name = Text(rt, item.displayName, new Vector2(0.5f, 0.88f), new Vector2(320, 36), 24, TextAnchor.MiddleCenter);
            name.color = new Color(1f, 0.9f, 0.5f);
            Text(rt, item.description, new Vector2(0.5f, 0.56f), new Vector2(310, 100), 17, TextAnchor.UpperCenter);
            var owned = Text(rt, "", new Vector2(0.5f, 0.30f), new Vector2(310, 26), 16, TextAnchor.MiddleCenter);
            owned.color = new Color(0.7f, 0.78f, 0.9f);

            var buyGo = new GameObject("Buy"); buyGo.transform.SetParent(rt, false);
            var buyImg = buyGo.AddComponent<Image>(); buyImg.color = new Color(0.16f, 0.38f, 0.24f, 0.95f);
            var brt = buyImg.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.13f); brt.pivot = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(300, 44);
            var buyBtn = buyGo.AddComponent<Button>(); buyBtn.targetGraphic = buyImg;
            var buyLabel = Text(brt, "", new Vector2(0.5f, 0.5f), new Vector2(300, 44), 19, TextAnchor.MiddleCenter);

            buyBtn.onClick.AddListener(() =>
            {
                if (run.gold < item.goldCost) return;
                run.gold -= item.goldCost;
                run.AddItem(item.name);
                GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                RefreshShop(run);
            });

            System.Action refresh = () =>
            {
                bool affordable = run.gold >= item.goldCost;
                owned.text = $"owned: {run.CountItem(item.name)}";
                buyLabel.text = $"BUY  —  {item.goldCost}g";
                buyLabel.color = affordable ? Color.white : new Color(1f, 0.55f, 0.5f);
                buyImg.color = affordable ? new Color(0.16f, 0.38f, 0.24f, 0.95f) : new Color(0.2f, 0.22f, 0.26f, 0.9f);
                buyBtn.interactable = affordable;
            };
            shopRefreshers.Add(refresh);
        }

        // Pick up to three distinct random boons, GUARANTEEING at least one rule-changing card.
        // Without the guarantee a draft can roll three stat bumps, and "+9 Defense vs +7 Attack vs
        // +5 Speed" is not a decision — it is the same card three times with different arithmetic.
        private List<BoonDefinition> PickThree()
        {
            var pool = new List<BoonDefinition>();
            foreach (var b in boonRoster)
                if (b != null && !boonsAlreadyOffered(b)) pool.Add(b);
            // Fisher–Yates partial shuffle.
            for (int i = 0; i < pool.Count; i++)
            {
                int j = Random.Range(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (pool.Count > 3) pool.RemoveRange(3, pool.Count - 3);

            // If the shuffle produced no rule card, swap the last slot for the first unused one.
            if (!pool.Exists(b => b.IsRule))
                foreach (var b in boonRoster)
                    if (b != null && b.IsRule && !pool.Contains(b) && !boonsAlreadyOffered(b))
                    {
                        pool[pool.Count - 1] = b;
                        break;
                    }
            return pool;
        }

        // Don't re-offer a boon the run already owns — a second copy of a rule card mostly does
        // nothing (the rules are booleans), so it would read as a dead slot.
        private bool boonsAlreadyOffered(BoonDefinition b)
        {
            var run = GameBootstrap.Instance != null ? GameBootstrap.Instance.Run : null;
            return run != null && b != null && b.IsRule && run.acquiredBoons.Contains(b.name);
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
            // Rewind to the state the player entered this boss with. Retrying from the corpse would
            // compound attrition every attempt until the fight was unwinnable by arithmetic.
            MakeBtn(panel, "Retry", 0.36f, () =>
            {
                GameBootstrap.Instance?.Run?.RestoreRetrySnapshot();
                Reload();
            });
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
            var img = go.AddComponent<Image>();
            // A rule card is a different KIND of choice from a stat card, so it reads as one at a
            // glance: warmer plate, gold title, and a "CHANGES THE RULES" banner.
            bool rule = boon.IsRule;
            img.color = rule ? new Color(0.26f, 0.21f, 0.12f, 0.97f) : new Color(0.15f, 0.22f, 0.34f, 0.97f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.42f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 300);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { GameBootstrap.Instance?.Audio?.PlaySfx("ui_click"); onClick(); });
            if (rule)
            {
                var banner = Text(rt, "CHANGES THE RULES", new Vector2(0.5f, 0.96f), new Vector2(330, 26), 15, TextAnchor.MiddleCenter);
                banner.color = new Color(1f, 0.78f, 0.25f);
            }
            var name = Text(rt, boon.displayName, new Vector2(0.5f, 0.85f), new Vector2(330, 50), 26, TextAnchor.MiddleCenter);
            name.color = rule ? new Color(1f, 0.82f, 0.35f) : new Color(1f, 0.9f, 0.5f);
            Text(rt, boon.description, new Vector2(0.5f, 0.41f), new Vector2(330, 200), 18, TextAnchor.UpperCenter);
        }

        private void Label(GameObject parent, string text, float anchorY, int size, Color color)
        {
            var t = Text((RectTransform)parent.transform, text, new Vector2(0.5f, anchorY), new Vector2(1500, size * 3 + 20), size, TextAnchor.MiddleCenter);
            t.color = color;
        }

        private Button MakeBtn(GameObject parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
            => MakeBtnAt(parent, label, 0.5f, anchorY, onClick);

        private Button MakeBtnAt(GameObject parent, string label, float anchorX, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.3f, 0.5f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 54);
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
