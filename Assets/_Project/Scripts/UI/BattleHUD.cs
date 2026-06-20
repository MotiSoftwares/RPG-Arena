using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Events;

namespace RPGArena.UI
{
    // The in-battle HUD. It is pure PRESENTATION: it reads runtime state and subscribes to the
    // SO event channels, and it only ever talks back to combat through BattleController.SubmitAction
    // (§4.2). Built entirely in code (no fragile scene wiring) with the built-in font, so it works
    // without TMP Essentials; the M4/M5 passes add TMP, icons, juice, and a click target-picker.
    public class BattleHUD : MonoBehaviour
    {
        [Header("Wiring")]
        public BattleController controller;     // found in the scene if left empty

        [Header("Channels (subscribed)")]
        public EntityChannel onTurnStarted, onStaggerBroken, onEntityDied;
        public DamageResultChannel onDamageDealt;
        public AbilityChannel onBossTelegraph;
        public Core.Events.VoidChannel onBattleWon, onBattleLost;

        private Font font;
        private Text bossName, bossHpText, log, telegraph, resultText;
        private Image bossHpFill, bossStaggerFill;
        private readonly List<Text> partyTexts = new();
        private readonly List<Image> partyHpFill = new();
        private readonly List<Image> partyMpFill = new();
        private RectTransform actionPanel;
        private GameObject resultPanel;
        private readonly List<string> logLines = new();

        private void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<BattleController>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
        }

        private void OnEnable()
        {
            onDamageDealt?.Subscribe(OnDamage);
            onStaggerBroken?.Subscribe(OnBreak);
            onBossTelegraph?.Subscribe(OnTelegraph);
            onEntityDied?.Subscribe(OnDied);
            onBattleWon?.Subscribe(_ => ShowResult("VICTORY!  The Dragon is slain.", new Color(0.2f, 0.8f, 0.3f)));
            onBattleLost?.Subscribe(_ => ShowResult("DEFEAT.  The party has fallen.", new Color(0.85f, 0.25f, 0.25f)));
        }

        private void OnDisable()
        {
            onDamageDealt?.Unsubscribe(OnDamage);
            onStaggerBroken?.Unsubscribe(OnBreak);
            onBossTelegraph?.Unsubscribe(OnTelegraph);
            onEntityDied?.Unsubscribe(OnDied);
        }

        private Entity lastMenuHero;

        private void Update()
        {
            if (controller == null || controller.Context == null) return;
            RefreshBars();

            // (Re)build the action menu when a player hero is awaiting input.
            if (controller.AwaitingInput && controller.ActiveHero != lastMenuHero)
            {
                lastMenuHero = controller.ActiveHero;
                BuildActionMenu(controller.ActiveHero);
            }
            else if (!controller.AwaitingInput && lastMenuHero != null)
            {
                lastMenuHero = null;
                ClearChildren(actionPanel);
            }
        }

        // --- channel handlers ---------------------------------------------------------
        private void OnDamage(DamageResult r)
        {
            string who = r.target != null ? r.target.displayName : "?";
            if (!r.hit) AddLog($"{who}: MISS");
            else if (r.absorbed) AddLog($"{who} ABSORBED {r.amount} (healed!)");
            else if (r.isHeal) AddLog($"{who} +{r.amount} HP");
            else AddLog($"{who} -{r.amount}{(r.reaction == ElementReaction.Weak ? " WEAK!" : "")}{(r.crit ? " CRIT" : "")}");
        }

        private void OnBreak(Entity boss) => AddLog($">>> BREAK! {boss.displayName} is staggered! <<<");
        private void OnDied(Entity e) => AddLog($"X {e.displayName} has fallen.");

        private void OnTelegraph(Ability a)
        {
            if (telegraph == null) return;
            telegraph.text = $"⚠ The Dragon is charging {a.displayName}!";
            telegraph.gameObject.SetActive(true);
            CancelInvoke(nameof(HideTelegraph));
            Invoke(nameof(HideTelegraph), 3.5f);
        }
        private void HideTelegraph() { if (telegraph) telegraph.gameObject.SetActive(false); }

        // --- per-frame UI refresh -----------------------------------------------------
        private void RefreshBars()
        {
            var ctx = controller.Context;
            if (ctx.boss != null)
            {
                bossName.text = ctx.boss.displayName;
                SetFill(bossHpFill, ctx.boss.currentHP, ctx.boss.stats.maxHP);
                SetFill(bossStaggerFill, ctx.boss.isStaggered ? ctx.boss.staggerThreshold : ctx.boss.staggerMeter, ctx.boss.staggerThreshold);
                bossHpText.text = $"HP {ctx.boss.currentHP}/{ctx.boss.stats.maxHP}";
            }
            for (int i = 0; i < partyTexts.Count; i++)
            {
                if (i >= ctx.heroes.Count) { partyTexts[i].transform.parent.gameObject.SetActive(false); continue; }
                var h = ctx.heroes[i];
                bool active = controller.ActiveHero == h;
                partyTexts[i].text = $"{(active ? "> " : "")}{h.displayName}  ({h.currentAttunement})";
                partyTexts[i].color = h.IsAlive ? (active ? Color.yellow : Color.white) : new Color(0.5f, 0.5f, 0.5f);
                SetFill(partyHpFill[i], h.currentHP, h.stats.maxHP);
                SetFill(partyMpFill[i], h.currentMP, h.stats.maxMP);
            }
        }

        private void BuildActionMenu(Entity hero)
        {
            ClearChildren(actionPanel);
            var ctx = controller.Context;
            float y = 0f;
            foreach (var ability in hero.abilities)
            {
                var ab = ability;
                bool affordable = hero.currentMP >= ab.mpCost && !hero.IsOnCooldown(ab);
                string label = $"{ab.displayName}";
                if (ab.mpCost > 0) label += $"  {ab.mpCost}MP";
                if (ab.cooldown > 0 && hero.IsOnCooldown(ab)) label += $"  (CD {hero.CooldownRemaining(ab)})";
                if (IsDamaging(ab)) label += $"   {Mathf.RoundToInt(EstimateHit(hero, ab, ctx.boss) * 100)}%";

                var btn = MakeButton(actionPanel, label, new Vector2(0, -y), affordable);
                if (affordable)
                    btn.onClick.AddListener(() => controller.SubmitAction(ab, PickTarget(hero, ab)));
                y += 30f;
            }
        }

        // --- helpers ------------------------------------------------------------------
        private static bool IsDamaging(Ability a) => a.effectType == EffectType.Attack || a.effectType == EffectType.MultiHit;

        private float EstimateHit(Entity hero, Ability a, Entity boss)
        {
            if (a.autoHit || a.hitTier == HitTier.Reliable) return 1f;
            var cfg = controller.balance;
            float tier = a.hitTier == HitTier.Risky ? cfg.riskyHitBase : cfg.standardHitBase;
            float eva = boss != null && !boss.isStaggered ? boss.Evasion : 0f;
            return Mathf.Clamp(tier + (hero.Accuracy - eva) * 0.01f, cfg.hitFloor, cfg.hitCeiling);
        }

        private Entity PickTarget(Entity hero, Ability a)
        {
            var ctx = controller.Context;
            switch (a.targetRule)
            {
                case TargetRule.SingleAlly: return TargetingSystem.LowestHP(ctx.heroes);
                case TargetRule.Self: return hero;
                default: return ctx.boss;   // SingleEnemy / AoE resolved by the controller
            }
        }

        private void AddLog(string line)
        {
            logLines.Add(line);
            while (logLines.Count > 9) logLines.RemoveAt(0);
            if (log) log.text = string.Join("\n", logLines);
        }

        private void ShowResult(string text, Color color)
        {
            resultPanel.SetActive(true);
            resultText.text = text; resultText.color = color;
        }

        private static void SetFill(Image img, float cur, float max)
        {
            if (img) img.fillAmount = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
        }

        // ============================ UI construction ================================
        private void BuildUI()
        {
            var canvasGo = new GameObject("BattleHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Boss panel (top).
            bossName = MakeText(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(700, 40), 28, TextAnchor.MiddleCenter);
            bossHpFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(700, 26), new Color(0.8f, 0.2f, 0.2f));
            bossHpText = MakeText(root, "HP", new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(700, 26), 16, TextAnchor.MiddleCenter);
            bossStaggerFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -106), new Vector2(700, 12), new Color(0.95f, 0.8f, 0.2f));
            telegraph = MakeText(root, "", new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 40), 24, TextAnchor.MiddleCenter);
            telegraph.color = new Color(1f, 0.5f, 0.1f);
            telegraph.gameObject.SetActive(false);

            // Party panel (bottom-left): up to 3 hero strips.
            for (int i = 0; i < 3; i++)
            {
                var strip = MakePanel(root, new Vector2(0f, 0f), new Vector2(20, 30 + i * 84), new Vector2(360, 78), new Color(0, 0, 0, 0.35f));
                var st = strip.GetComponent<RectTransform>();
                partyTexts.Add(MakeText(st, "Hero", new Vector2(0f, 1f), new Vector2(110, -4), new Vector2(240, 22), 18, TextAnchor.MiddleLeft));
                partyHpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(118, -30), new Vector2(230, 16), new Color(0.3f, 0.8f, 0.3f)));
                partyMpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(118, -52), new Vector2(230, 12), new Color(0.3f, 0.5f, 0.9f)));
            }

            // Action menu (bottom-right).
            var menu = MakePanel(root, new Vector2(1f, 0f), new Vector2(-20, 30), new Vector2(380, 320), new Color(0, 0, 0, 0.35f));
            actionPanel = menu.GetComponent<RectTransform>();

            // Combat log (left-middle).
            log = MakeText(root, "", new Vector2(0f, 0.5f), new Vector2(20, 60), new Vector2(420, 220), 15, TextAnchor.LowerLeft);

            // Result overlay (hidden until end).
            resultPanel = MakePanel(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 240), new Color(0, 0, 0, 0.8f));
            resultPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            resultText = MakeText(resultPanel.GetComponent<RectTransform>(), "", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 60), 34, TextAnchor.MiddleCenter);
            var menuBtn = MakeButton(resultPanel.GetComponent<RectTransform>(), "Return to Main Menu", new Vector2(0, -40), true);
            var mbr = menuBtn.GetComponent<RectTransform>();
            mbr.anchorMin = mbr.anchorMax = new Vector2(0.5f, 0.5f); mbr.anchoredPosition = new Vector2(0, -50);
            menuBtn.onClick.AddListener(() => { var b = GameBootstrap.Instance; if (b != null) b.Scenes.LoadScene("MainMenu"); });
            resultPanel.SetActive(false);
        }

        private Text MakeText(RectTransform parent, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = content; t.fontSize = fontSize; t.alignment = align; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }

        private Image MakeBar(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var bg = new GameObject("BarBG"); bg.transform.SetParent(parent, false);
            var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(0, 0, 0, 0.6f);
            var brt = bgImg.rectTransform; brt.anchorMin = brt.anchorMax = anchor; brt.pivot = anchor; brt.anchoredPosition = pos; brt.sizeDelta = size;
            var fg = new GameObject("BarFill"); fg.transform.SetParent(bg.transform, false);
            var img = fg.AddComponent<Image>(); img.color = color; img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f;
            var frt = img.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            return img;
        }

        private GameObject MakePanel(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 pos, bool enabled)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = enabled ? new Color(0.16f, 0.3f, 0.5f, 0.9f) : new Color(0.25f, 0.25f, 0.25f, 0.7f);
            var rt = img.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(0, 28); rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = enabled;
            var t = MakeText(rt, label, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(340, 26), 15, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-6, 0);
            return btn;
        }

        private static void ClearChildren(RectTransform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }
    }
}
