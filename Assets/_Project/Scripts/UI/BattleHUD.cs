using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Status;
using RPGArena.Combat.Events;
using RPGArena.Combat.Commands;   // RiskStake — the stake the player sets on a SPECIAL

namespace RPGArena.UI
{
    // The in-battle HUD. Pure PRESENTATION: it reads runtime state and subscribes to the SO event
    // channels, and only talks back to combat through BattleController.SubmitAction (§4.2). Built
    // entirely in code (no fragile scene wiring) and reskinned in a cohesive SlimUI-style theme —
    // TextMeshPro SDF fonts (Poppins/Rubik), a dark-slate palette with teal/gold accents, rounded
    // framed panels, two-line ability cards, and animated chip-away bars.
    public class BattleHUD : MonoBehaviour
    {
        [Header("Wiring")]
        public BattleController controller;     // found in the scene if left empty

        [Header("Fonts (SlimUI SDF; falls back to the TMP default if unset)")]
        public TMP_FontAsset fontHeader;        // Poppins-Bold SDF — names / headers
        public TMP_FontAsset fontBody;          // Rubik-Medium SDF — values / body

        [Header("Channels (subscribed)")]
        public EntityChannel onTurnStarted, onStaggerBroken, onEntityDied;
        public DamageResultChannel onDamageDealt;
        public AbilityChannel onBossTelegraph;
        public Core.Events.VoidChannel onBattleWon, onBattleLost;
        public Core.Events.StringChannel onAnnouncement;   // centre-screen play-by-play from the controller

        // ---- Theme -------------------------------------------------------------------
        static readonly Color Panel    = new Color(0.055f, 0.07f, 0.105f, 0.90f);   // dark slate
        static readonly Color PanelLit = new Color(0.10f, 0.13f, 0.19f, 0.94f);     // raised card
        static readonly Color Stroke   = new Color(0.55f, 0.70f, 0.95f, 0.10f);     // hairline edge
        static readonly Color TxtMain  = new Color(0.92f, 0.95f, 0.99f);
        static readonly Color TxtMuted = new Color(0.56f, 0.62f, 0.74f);
        static readonly Color Accent   = new Color(0.27f, 0.78f, 0.92f);            // teal
        static readonly Color Gold     = new Color(0.98f, 0.80f, 0.32f);
        static readonly Color Danger   = new Color(0.90f, 0.32f, 0.27f);
        static readonly Color HpGreen  = new Color(0.36f, 0.82f, 0.46f);
        static readonly Color MpBlue   = new Color(0.34f, 0.56f, 0.96f);
        static readonly Color Track    = new Color(0f, 0f, 0f, 0.55f);

        private TMP_Text bossName, bossHpText, log, telegraph, bossWeakness, bossStaggerText, coachCaption, valorText, menuTitle, bossFuryText;
        private GameObject bossFuryPanel, logPanel;
        private bool coachDone;
        private Image bossHpFill, bossStaggerFill, valorFill;
        private GameObject telegraphPanel, coachPanel;
        private Sprite roundedSprite, softSprite;
        private bool weaknessSeen;
        private readonly List<TMP_Text> partyName = new();
        private readonly List<Image> partyHpFill = new();
        private readonly List<Image> partyMpFill = new();
        private readonly List<TMP_Text> partyHpText = new();
        private readonly List<TMP_Text> partyMpText = new();
        private readonly List<Image> partyActiveStripe = new();
        private readonly List<Image> partyCardBg = new();
        private RectTransform actionPanel;
        private struct LogEntry { public string text; public Color color; }
        private readonly List<LogEntry> logEntries = new();
        private RectTransform bossStatusRow;
        private readonly List<RectTransform> heroStatusRows = new();
        private readonly string[] statusSigs = new string[8];
        private RectTransform turnOrderRow;
        private string turnOrderSig = "";
        private readonly Dictionary<Image, Image> ghostOf = new();
        private readonly Dictionary<Image, float> targetFill = new();

        private void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<BattleController>();
            juice = FindFirstObjectByType<JuiceController>();
            if (fontHeader == null) fontHeader = TMP_Settings.defaultFontAsset;
            if (fontBody == null) fontBody = fontHeader;
            BuildUI();
        }

        private void OnEnable()
        {
            onDamageDealt?.Subscribe(OnDamage);
            onStaggerBroken?.Subscribe(OnBreak);
            onBossTelegraph?.Subscribe(OnTelegraph);
            onEntityDied?.Subscribe(OnDied);
            onAnnouncement?.Subscribe(OnAnnounce);
        }

        private void OnDisable()
        {
            onDamageDealt?.Unsubscribe(OnDamage);
            onStaggerBroken?.Unsubscribe(OnBreak);
            onBossTelegraph?.Unsubscribe(OnTelegraph);
            onEntityDied?.Unsubscribe(OnDied);
            onAnnouncement?.Unsubscribe(OnAnnounce);
        }

        // --- the announcer: centre-screen play-by-play ----------------------------------
        // Every action and every phase change is spelled out where the player is already looking,
        // so no turn is ever a mystery. Latest line replaces the previous (no queue backlog).
        private TMP_Text announceText;
        private Image announceBg;
        private Coroutine announceCo;

        private void OnAnnounce(string line)
        {
            if (announceText == null || string.IsNullOrEmpty(line)) return;
            if (announceCo != null) StopCoroutine(announceCo);
            announceCo = StartCoroutine(AnnounceRoutine(line));
        }

        private IEnumerator AnnounceRoutine(string line)
        {
            bool enemyBeat = line.Contains("ENEMY PHASE") || line.Contains("BROKEN") || line.Contains("FROZEN");
            bool yourBeat = line.Contains("YOUR PHASE");
            announceText.text = line;
            announceText.color = yourBeat ? Accent : enemyBeat ? new Color(1f, 0.62f, 0.5f) : new Color(1f, 0.96f, 0.86f);
            announceBg.gameObject.SetActive(true);

            // Pop in, hold, fade — all on unscaled time so hit-stop can't freeze the caption.
            float t = 0f;
            while (t < 0.14f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.14f);
                announceBg.transform.localScale = new Vector3(1f, k, 1f);
                SetAlpha(1f);
                yield return null;
            }
            announceBg.transform.localScale = Vector3.one;
            t = 0f;
            while (t < 1.35f) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(1f - t / 0.35f);
                yield return null;
            }
            announceBg.gameObject.SetActive(false);
            SetAlpha(1f);
            announceCo = null;

            void SetAlpha(float a)
            {
                var c = announceText.color; c.a = a; announceText.color = c;
                var b = announceBg.color; b.a = 0.72f * a; announceBg.color = b;
            }
        }

        private Entity lastMenuHero;
        private GameObject canvasRoot;      // whole HUD canvas — hidden during the intro cinematic
        private IBattleIntro intro;
        private bool introChecked;
        private JuiceController juice;      // for PERFECT!/BRACED! floaters
        private GameObject bracePanel;      // the defensive action-command prompt
        private GameObject menuPanelGo;     // the whole action-menu panel (hidden between hero turns)
        private Image braceImg;
        private TMP_Text braceText;
        private bool braceShownLanded;
        private Coroutine timingCo;         // the offensive action-command bar

        private void Update()
        {
            if (controller == null || controller.Context == null) return;

            // Stay hidden while a pre-fight cinematic owns the screen; pop in when it hands off.
            if (!introChecked)
            {
                introChecked = true;
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb is IBattleIntro bi) { intro = bi; break; }
            }
            if (intro != null && !intro.IsIntroDone)
            {
                if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
                return;
            }
            if (canvasRoot != null && !canvasRoot.activeSelf) canvasRoot.SetActive(true);

            UpdateBracePrompt();
            UpdateFollowUpPrompt();
            RefreshBars();
            AnimateBars();
            RefreshAdvisor();

            // PLAYER PHASE, step 1: the controller wants to know WHICH hero acts next.
            if (controller.AwaitingHeroPick)
            {
                if (!pickShowing)
                {
                    pickShowing = true;
                    lastMenuHero = null;
                    if (menuPanelGo != null) menuPanelGo.SetActive(true);
                    BuildHeroPickMenu();
                }
            }
            else pickShowing = false;

            // PLAYER PHASE, step 2: a hero is selected — show their action menu.
            if (controller.AwaitingInput && controller.ActiveHero != lastMenuHero)
            {
                lastMenuHero = controller.ActiveHero;
                if (menuPanelGo != null) menuPanelGo.SetActive(true);
                BuildActionMenu(controller.ActiveHero);
                if (!coachDone && coachPanel != null) coachPanel.SetActive(true);
            }
            else if (!controller.AwaitingInput && !controller.AwaitingHeroPick && lastMenuHero != null)
            {
                lastMenuHero = null;
                ClearChildren(actionPanel);
                if (menuTitle != null) menuTitle.text = "";
                if (menuPanelGo != null) menuPanelGo.SetActive(false);   // no empty box during enemy turns
                if (!coachDone && coachPanel != null) { coachPanel.SetActive(false); coachDone = true; }
            }
        }

        private bool pickShowing;

        // "CHOOSE YOUR HERO" — the player decides who acts next, in any order, each hero once.
        // This is the round's first decision: detonators want to move AFTER the setup lands.
        private void BuildHeroPickMenu()
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = "YOUR PHASE — WHO ACTS NEXT?";
            float y = 0f;
            foreach (var h in controller.HeroesYetToAct)
            {
                if (h == null || !h.IsAlive) continue;
                var captured = h;
                var btn = MakeSimpleButton(actionPanel,
                    $"»  {h.displayName}    <size=75%><color=#8FE38F>{h.currentHP}/{h.stats.maxHP} HP</color>   <color=#7FA8F0>{h.currentMP}/{h.stats.maxMP} MP</color></size>",
                    new Vector2(0, -y), Accent, true);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.richText = true;
                btn.onClick.AddListener(() =>
                {
                    GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                    controller.SelectHero(captured);
                });
                y += 40f;
            }
        }

        // --- channel handlers ---------------------------------------------------------
        private void OnDamage(DamageResult r)
        {
            string who = r.target != null ? r.target.displayName : "?";
            if (!r.hit) AddLog($"{who}  miss", TxtMuted);
            else if (r.glanced) AddLog($"{who}  -{r.amount}  graze", TxtMuted);
            else if (r.absorbed) AddLog($"{who}  ABSORBED {r.amount} — healed", new Color(0.55f, 0.80f, 1f));
            else if (r.isHeal) AddLog($"{who}  +{r.amount} HP", HpGreen);
            else
            {
                string suffix = (r.reaction == ElementReaction.Weak ? "  WEAK" : "") + (r.crit ? "  CRIT" : "");
                Color c = r.crit ? Gold
                        : r.reaction == ElementReaction.Weak ? Accent
                        : new Color(0.84f, 0.86f, 0.90f);
                AddLog($"{who}  -{r.amount}{suffix}", c);
            }
            if (r.reaction == ElementReaction.Weak) weaknessSeen = true;
        }

        private void OnBreak(Entity boss) => AddLog($"BREAK!  {boss.displayName} staggered", new Color(1f, 0.68f, 0.18f));
        private void OnDied(Entity e) => AddLog($"{e.displayName} has fallen", new Color(1f, 0.40f, 0.38f));

        private void OnTelegraph(Ability a)
        {
            if (telegraph == null) return;
            string who = controller != null && controller.Context != null && controller.Context.boss != null
                ? controller.Context.boss.displayName.ToUpper() : "THE BOSS";
            // GLYPHS: the SlimUI SDF atlases are Latin-only. Poppins-Bold carries nothing decorative
            // beyond the em dash, and Rubik-Medium adds only ↕ ← » • × –. Anything else — ⚠ ★ ◆ ⚔ ✖ ⏳
            // — renders as a tofu box, which is exactly what this warning had been doing.
            telegraph.text = $"!!  {who} IS CHARGING: {a.displayName.ToUpper()}  —  BREAK IT OR DEFEND  !!";
            if (telegraphPanel != null) telegraphPanel.SetActive(true);
        }
        private void HideTelegraphNow() { if (telegraphPanel != null) telegraphPanel.SetActive(false); }

        // --- per-frame UI refresh -----------------------------------------------------
        private void RefreshBars()
        {
            var ctx = controller.Context;
            if (ctx.boss != null)
            {
                bossName.text = ctx.boss.displayName;
                SetFill(bossHpFill, ctx.boss.currentHP, ctx.boss.stats.maxHP);
                SetFill(bossStaggerFill, ctx.boss.isStaggered ? ctx.boss.staggerThreshold : ctx.boss.staggerMeter, ctx.boss.staggerThreshold);
                bossHpText.text = $"{ctx.boss.currentHP} / {ctx.boss.stats.maxHP}";
                if (bossStaggerText != null)
                    bossStaggerText.text = ctx.boss.isStaggered
                        ? "—  BROKEN  —"
                        : $"BREAK  {Mathf.RoundToInt(ctx.boss.staggerMeter)} / {Mathf.RoundToInt(ctx.boss.staggerThreshold)}";
                if (bossStaggerFill != null)
                    bossStaggerFill.color = ctx.boss.isStaggered
                        ? Color.Lerp(new Color(1f, 0.96f, 0.6f), Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f))
                        : Gold;
                if (bossWeakness != null)
                    bossWeakness.text = (weaknessSeen || ctx.weaknessRevealed) ? FormatWeakness(ctx.boss) : $"<color=#7A8398>study {ctx.boss.displayName.ToLower()} to reveal its weakness</color>";
                RefreshStatusRow(bossStatusRow, ctx.boss, 0, true);

                // Searing Fury: the escalating-damage warning that makes Break essential.
                int rage = ctx.boss.rageStacks;
                if (bossFuryPanel != null)
                {
                    bool show = rage > 0 && !ctx.boss.isStaggered;
                    if (bossFuryPanel.activeSelf != show) bossFuryPanel.SetActive(show);
                    if (show && bossFuryText != null)
                    {
                        float pct = rage * (controller.balance != null ? controller.balance.rageDamagePerStack : 0.06f) * 100f;
                        float hot = Mathf.Clamp01(rage / 8f);   // redder + pulsing as it climbs
                        Color c = Color.Lerp(new Color(1f, 0.72f, 0.36f), new Color(1f, 0.30f, 0.20f), hot);
                        if (hot > 0.6f) c = Color.Lerp(c, Color.white, Mathf.PingPong(Time.unscaledTime * 5f, 1f) * 0.5f);
                        bossFuryText.color = c;
                        bossFuryText.text = $"SEARING FURY  ×{rage}    +{pct:0}% DMG";
                    }
                }

                bool charging = ctx.boss.telegraphedAbility != null;
                if (!charging && telegraphPanel != null && telegraphPanel.activeSelf) HideTelegraphNow();
            }

            var charge = ctx.charge;
            if (valorFill != null && charge != null)
            {
                SetFill(valorFill, charge.valor, charge.max);
                if (valorText != null)
                    valorText.text = charge.overdriveActive ? $"—  OVERDRIVE  {charge.overdriveTurnsLeft}  —"
                                   : charge.IsFull ? "VALOR FULL — unleash OVERDRIVE!"
                                   : $"VALOR   {Mathf.RoundToInt(charge.valor)} / {Mathf.RoundToInt(charge.max)}";
                valorFill.color = (charge.IsFull || charge.overdriveActive)
                    ? Color.Lerp(Gold, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f))
                    : Gold;
            }

            RefreshTurnOrder();
            for (int i = 0; i < partyName.Count; i++)
            {
                if (i >= ctx.heroes.Count) { partyName[i].transform.parent.gameObject.SetActive(false); continue; }
                var h = ctx.heroes[i];
                bool active = controller.ActiveHero == h;
                partyName[i].text = $"{h.displayName}  <size=60%><color=#7A8398>{(h.backRow ? "BACK" : "FRONT")}</color></size>";
                partyName[i].color = h.IsAlive ? (active ? Gold : TxtMain) : TxtMuted;
                SetFill(partyHpFill[i], h.currentHP, h.stats.maxHP);
                SetFill(partyMpFill[i], h.currentMP, h.stats.maxMP);
                if (i < partyHpText.Count) partyHpText[i].text = h.IsAlive ? $"{h.currentHP}/{h.stats.maxHP}" : "KO";
                if (i < partyMpText.Count) partyMpText[i].text = $"{h.currentMP}/{h.stats.maxMP}";
                if (i < partyActiveStripe.Count && partyActiveStripe[i] != null)
                    partyActiveStripe[i].color = active ? Gold : new Color(Gold.r, Gold.g, Gold.b, 0f);
                if (i < partyCardBg.Count && partyCardBg[i] != null)
                    partyCardBg[i].color = active ? PanelLit : Panel;
                if (i < heroStatusRows.Count) RefreshStatusRow(heroStatusRows[i], h, i + 1, false);
            }
        }

        // --- phase tracker --------------------------------------------------------------
        // The old initiative queue is gone; the panel now mirrors the two-phase round exactly:
        // your heroes still to act (gold = acting now), then every enemy with its INTENT. Nothing
        // on this list ever silently disappears — heroes leave it by acting, enemies by dying.
        private void RefreshTurnOrder()
        {
            if (turnOrderRow == null || controller.Context == null) return;

            var heroes = controller.HeroesYetToAct;
            var enemies = new List<Entity>();
            foreach (var m in controller.Context.minions) if (m != null && m.IsAlive) enemies.Add(m);
            if (controller.Context.boss != null && controller.Context.boss.IsAlive) enemies.Add(controller.Context.boss);

            var sb = new System.Text.StringBuilder();
            foreach (var h in heroes) if (h != null && h.IsAlive) sb.Append(h.displayName).Append(controller.ActiveHero == h ? "*" : "").Append('|');
            sb.Append("::");
            foreach (var e in enemies) sb.Append(e.displayName).Append(':').Append(IntentOf(e)).Append('|');
            string sig = sb.ToString();
            if (sig == turnOrderSig) return;
            turnOrderSig = sig;

            ClearChildren(turnOrderRow);
            turnOrderCursor = 0;

            bool playerPhase = heroes != null && heroes.Count > 0;
            TrackerHeader(playerPhase ? "YOUR PHASE" : "ENEMY PHASE", playerPhase ? Accent : Danger);
            if (playerPhase)
                foreach (var h in heroes)
                {
                    if (h == null || !h.IsAlive) continue;
                    bool now = controller.ActiveHero == h;
                    TrackerChip((now ? "NOW  " : "»  ") + h.displayName, "", false,
                        now ? new Color(Gold.r, Gold.g, Gold.b, 0.22f) : new Color(0.15f, 0.2f, 0.32f, 0.55f),
                        now ? Gold : Accent, now ? Gold : TxtMain);
                }

            TrackerHeader(playerPhase ? "THEN — ENEMY PHASE" : "ACTING NOW", new Color(1f, 0.55f, 0.45f));
            foreach (var e in enemies)
            {
                string intent = IntentOf(e);
                TrackerChip(e.displayName, intent, intent.Length > 0,
                    e.isBoss ? new Color(0.5f, 0.16f, 0.16f, 0.55f) : new Color(0.30f, 0.16f, 0.16f, 0.55f),
                    Danger, TxtMain);
            }
        }

        private int turnOrderCursor;

        private void TrackerHeader(string label, Color color)
        {
            var t = MakeText(turnOrderRow, label, fontBody, 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            var trt = t.rectTransform; trt.anchoredPosition = new Vector2(0, -turnOrderCursor); trt.sizeDelta = new Vector2(166, 18);
            t.color = color; t.characterSpacing = 6f;
            turnOrderCursor += 21;
        }

        private void TrackerChip(string name, string intent, bool tall, Color bg, Color dotColor, Color nameColor)
        {
            var chip = new GameObject("Chip"); chip.transform.SetParent(turnOrderRow, false);
            var img = chip.AddComponent<Image>(); Soft(img); img.color = bg;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -turnOrderCursor); rt.sizeDelta = new Vector2(166, tall ? 40 : 27);
            turnOrderCursor += tall ? 43 : 30;

            var dot = new GameObject("Dot"); dot.transform.SetParent(rt, false);
            var dimg = dot.AddComponent<Image>(); Soft(dimg); dimg.color = dotColor;
            var drt = dimg.rectTransform; drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f); drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(10, tall ? -9 : -10); drt.sizeDelta = new Vector2(8, 8);

            var t = MakeText(rt, name, fontBody, 14, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1));
            var trt = t.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0, 1); trt.anchoredPosition = new Vector2(24, -3); trt.sizeDelta = new Vector2(-28, 18);
            t.color = nameColor;

            // INTENT: what this enemy is about to do. Planning beats reacting — the whole fight
            // changes character once you can see the incoming blow one turn early.
            if (tall)
            {
                var it = MakeText(rt, intent, fontBody, 11.5f, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1));
                var irt = it.rectTransform; irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(1, 1);
                irt.pivot = new Vector2(0, 1); irt.anchoredPosition = new Vector2(24, -21); irt.sizeDelta = new Vector2(-28, 16);
                it.richText = true; it.enableWordWrapping = false; it.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // A short, readable description of an enemy's NEXT action. A committed telegraph outranks
        // everything (it is already locked in); otherwise we ask the brain for a side-effect-free
        // preview. Brains that are genuinely random answer null, and "???" is honest information.
        private string IntentOf(Entity e)
        {
            if (e == null || e.team != Team.Enemies || controller == null || controller.Context == null) return "";
            if (e.telegraphedAbility != null)
                return $"<color=#FF7A5C>! {e.telegraphedAbility.displayName.ToUpper()}</color>";
            var brain = e.Brain;
            if (brain == null) return "";
            Ability next = null;
            try { next = brain.PreviewIntent(controller.Context, e, controller.Context.heroes); }
            catch { next = null; }
            if (next == null) return "<color=#8A95A8>??? unpredictable</color>";
            if (next.consumesSetupFlags) return "<color=#FF7A5C>! DEVOURS YOUR SETUPS</color>";

            string scope = next.targetRule == TargetRule.AllEnemies ? "  <color=#FF9E7A>ALL</color>" : "";
            string col = next.effectType == EffectType.BossMove ? "#FFD24A"
                       : (next.effectType == EffectType.Attack || next.effectType == EffectType.MultiHit) ? "#E8907A"
                       : "#8FD8A0";
            return $"<color={col}>{next.displayName}</color>{scope}";
        }

        // --- status-effect badges -----------------------------------------------------
        private void RefreshStatusRow(RectTransform row, Entity e, int sigKey, bool big)
        {
            if (row == null || e == null) return;
            string sig = StatusSig(e);
            if (sig == statusSigs[sigKey]) return;
            statusSigs[sigKey] = sig;
            ClearChildren(row);
            var fx = e.Status.Effects;
            float step = big ? 64f : 48f;
            float startX = big ? Mathf.Max(0f, (row.sizeDelta.x - fx.Count * step) / 2f) : 0f;
            for (int i = 0; i < fx.Count; i++) MakeBadge(row, fx[i], startX + i * step, big);
        }

        private void MakeBadge(RectTransform parent, StatusEffectContainer.Active a, float x, bool big)
        {
            float w = big ? 60f : 44f, h = big ? 22f : 16f;
            var go = new GameObject("Badge"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); Soft(img); img.color = KindColor(a.def.kind);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, h);
            string lbl = Abbrev(a.def.displayName) + (a.stacks > 1 ? a.stacks.ToString() : "") + (a.remaining < 90 ? " " + a.remaining : "");
            var t = MakeText(rt, lbl, fontBody, big ? 12 : 10, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(t, 0, 0, 0, 0);
            t.color = Color.white;
        }

        private static string Abbrev(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, Mathf.Min(3, name.Length)).ToUpper();

        private static Color KindColor(StatusKind k) =>
            k == StatusKind.Buff ? new Color(0.24f, 0.66f, 0.34f, 0.92f) :
            k == StatusKind.Debuff ? new Color(0.8f, 0.3f, 0.3f, 0.92f) :
            k == StatusKind.DoT ? new Color(0.9f, 0.55f, 0.2f, 0.92f) :
            k == StatusKind.Control ? new Color(0.55f, 0.35f, 0.85f, 0.92f) :
            new Color(0.28f, 0.6f, 0.85f, 0.92f);

        private string StatusSig(Entity e)
        {
            var sb = new System.Text.StringBuilder();
            var fx = e.Status.Effects;
            for (int i = 0; i < fx.Count; i++)
                sb.Append(fx[i].def.displayName).Append(fx[i].stacks).Append('x').Append(fx[i].remaining).Append('|');
            return sb.ToString();
        }

        private RectTransform MakeRow(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Row"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        // --- action commands ----------------------------------------------------------
        // Defensive: BLOCK is a timed skill now, not a free window. While the enemy winds up, a
        // fast needle ping-pongs over a small gold core — press SPACE ON GOLD for a PERFECT BLOCK
        // (×0.55), near it for a BLOCK (×0.78); a bad press FUMBLES the guard (full damage) and a
        // no-press eats the hit. The sweep speed is randomised per attack so it cannot be metronomed:
        // reading the needle IS the skill, and a good block feels earned.
        private Coroutine braceCo;

        private void UpdateBracePrompt()
        {
            if (bracePanel == null || controller == null) return;
            if (controller.BraceWindowOpen && braceCo == null)
                braceCo = StartCoroutine(BraceBarRoutine());
        }

        private IEnumerator BraceBarRoutine()
        {
            bracePanel.SetActive(true);
            bracePanel.transform.localScale = Vector3.one;
            braceText.text = "";
            foreach (Transform child in bracePanel.transform)
                if (child.gameObject.name == "BlockTrack") Destroy(child.gameObject);

            // Build the mini needle track inside the brace panel.
            var trackGo = new GameObject("BlockTrack"); trackGo.transform.SetParent(bracePanel.transform, false);
            var track = trackGo.AddComponent<Image>(); Soft(track); track.color = new Color(0.05f, 0.05f, 0.09f, 0.97f); track.raycastTarget = false;
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0f); trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -6); trt.sizeDelta = new Vector2(300, 30);
            System.Action<float, float, Color> zone = (min, max, col) =>
            {
                var z = new GameObject("Zone"); z.transform.SetParent(trt, false);
                var zi = z.AddComponent<Image>(); Soft(zi); zi.color = col; zi.raycastTarget = false;
                var zrt = zi.rectTransform;
                zrt.anchorMin = new Vector2(min, 0.15f); zrt.anchorMax = new Vector2(max, 0.85f);
                zrt.offsetMin = zrt.offsetMax = Vector2.zero;
            };
            zone(0.34f, 0.66f, new Color(Accent.r, Accent.g, Accent.b, 0.30f));
            zone(0.46f, 0.54f, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            var needleGo = new GameObject("Needle"); needleGo.transform.SetParent(trt, false);
            var needle = needleGo.AddComponent<Image>(); needle.color = Color.white; needle.raycastTarget = false;
            var nrt = needle.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0f, 1f);
            nrt.sizeDelta = new Vector2(4f, 0f); nrt.anchoredPosition = Vector2.zero;

            braceText.text = "!!  BLOCK ON GOLD  —  SPACE  !!";
            braceText.color = new Color(1f, 0.93f, 0.5f);
            if (braceImg != null) braceImg.color = new Color(0.32f, 0.06f, 0.05f, 0.95f);

            // Random sweep speed per attack — a metronome press cannot cheese it.
            float speed = Random.Range(1.35f, 1.95f);   // full track lengths per second
            float t = Random.Range(0f, 0.6f);           // random start phase too
            bool pressed = false; float pressPos = 0f;

            while (controller.BraceWindowOpen && !pressed)
            {
                if (PauseMenu.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime * speed;
                float pos = Mathf.PingPong(t, 1f);
                nrt.anchorMin = new Vector2(pos, 0f); nrt.anchorMax = new Vector2(pos, 1f);
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame) { pressed = true; pressPos = pos; }
                yield return null;
            }

            if (pressed)
            {
                float off = Mathf.Abs(pressPos - 0.5f);
                if (off < 0.06f)
                {
                    controller.SubmitBrace(0.55f);
                    braceText.text = "PERFECT BLOCK!";
                    braceText.color = new Color(1f, 0.9f, 0.4f);
                    if (braceImg != null) braceImg.color = new Color(0.30f, 0.24f, 0.05f, 0.95f);
                    GameBootstrap.Instance?.Audio?.PlaySfx("crit");
                }
                else if (off < 0.18f)
                {
                    controller.SubmitBrace(0.78f);
                    braceText.text = "BLOCKED!";
                    braceText.color = new Color(0.5f, 1f, 0.6f);
                    if (braceImg != null) braceImg.color = new Color(0.08f, 0.30f, 0.14f, 0.95f);
                    GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                }
                else
                {
                    controller.SubmitBrace(1f);   // consumed the attempt, blocked nothing
                    braceText.text = "FUMBLED!";
                    braceText.color = new Color(1f, 0.5f, 0.45f);
                    if (braceImg != null) braceImg.color = new Color(0.32f, 0.08f, 0.06f, 0.95f);
                }
                Destroy(trackGo);
                // Manual unscaled wait — WaitForSecondsRealtime never resumes under an unfocused
                // editor's Step loop (the documented timeScale-stall gotcha).
                float hold = 0f;
                while (hold < 0.55f) { hold += Time.unscaledDeltaTime; yield return null; }
            }
            else
            {
                Destroy(trackGo);
            }

            bracePanel.SetActive(false);
            braceCo = null;
        }

        // FOLLOW-UP: a surprise opening after a landed blow. The prompt appears at a random beat
        // and lives ~0.3s — pure reaction. Pressing it lands a bonus echo strike.
        private GameObject followPanel;
        private TMP_Text followText;

        private void UpdateFollowUpPrompt()
        {
            if (followPanel == null || controller == null) return;
            bool open = controller.FollowUpPromptOpen;
            if (open && !followPanel.activeSelf)
            {
                followPanel.SetActive(true);
                followText.text = "!!  OPENING — SPACE  !!";
            }
            else if (!open && followPanel.activeSelf) followPanel.SetActive(false);

            if (open)
            {
                followPanel.transform.localScale = Vector3.one * (1f + 0.10f * Mathf.Sin(Time.unscaledTime * 22f));
                var kb = Keyboard.current;
                if (!PauseMenu.IsPaused && kb != null && kb.spaceKey.wasPressedThisFrame)
                {
                    controller.SubmitFollowUp();
                    GameBootstrap.Instance?.Audio?.PlaySfx("crit");
                }
            }
        }

        // Offensive: the timed-strike bar. The needle sweeps once; lock it on gold for a PERFECT
        // (x1.18 damage AND stagger — timing feeds basePower), teal for normal, anything else is
        // sloppy (x0.9). No input = sloppy. The multiplier rides the ActionRequest into logic.
        private void StartTimingBar(Entity hero, Ability ab, Entity target)
            => StartTimingBar(hero, ab, target, RiskStake.Press);

        private void StartTimingBar(Entity hero, Ability ab, Entity target, RiskStake stake)
        {
            if (timingCo != null) StopCoroutine(timingCo);
            timingCo = StartCoroutine(TimingBarRoutine(hero, ab, target, stake));
        }

        private IEnumerator TimingBarRoutine(Entity hero, Ability ab, Entity target, RiskStake stake)
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = $"{ab.displayName.ToUpper()} — STRIKE ON GOLD!";

            var trackGo = new GameObject("Track"); trackGo.transform.SetParent(actionPanel, false);
            var track = trackGo.AddComponent<Image>(); Soft(track); track.color = new Color(0.05f, 0.07f, 0.11f, 0.97f);
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -34); trt.sizeDelta = new Vector2(-12, 66);

            System.Action<float, float, Color> makeZone = (min, max, col) =>
            {
                var z = new GameObject("Zone"); z.transform.SetParent(trt, false);
                var zi = z.AddComponent<Image>(); Soft(zi); zi.color = col; zi.raycastTarget = false;
                var zrt = zi.rectTransform;
                zrt.anchorMin = new Vector2(min, 0.12f); zrt.anchorMax = new Vector2(max, 0.88f);
                zrt.offsetMin = zrt.offsetMax = Vector2.zero;
            };
            makeZone(0.32f, 0.68f, new Color(Accent.r, Accent.g, Accent.b, 0.30f));   // GOOD band
            makeZone(0.45f, 0.55f, new Color(Gold.r, Gold.g, Gold.b, 0.85f));         // PERFECT core

            var needleGo = new GameObject("Needle"); needleGo.transform.SetParent(trt, false);
            var needle = needleGo.AddComponent<Image>(); needle.color = Color.white; needle.raycastTarget = false;
            var nrt = needle.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0f, 1f);
            nrt.sizeDelta = new Vector2(5f, 0f); nrt.anchoredPosition = Vector2.zero;

            var hint = MakeText(actionPanel, "the needle passes gold TWICE — out, then back. Strike on gold (+18%)", fontBody, 13, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(hint, new Vector2(0, -108), new Vector2(400, 20)); hint.color = TxtMuted;

            // ONE pass out, ONE pass back, then it resolves. Two chances at a SMALLER gold makes the
            // rhythm readable on a first playthrough (you watch the out-pass, you strike the return)
            // while the tighter core keeps PERFECT an earned hit rather than a default.
            const float sweep = 0.8f;        // seconds per direction
            const float total = sweep * 2f;
            const float armAfter = 0.15f;    // grace: the click/keypress that OPENED the bar must not lock it
            float t = 0f, pos = 0f;
            bool locked = false;

            var btn = trackGo.AddComponent<Button>(); btn.targetGraphic = track;
            // The Track sits exactly where the ability cards were, and those are only destroyed at
            // end of frame — without the grace, a habitual double-click locks at pos~0.2 (SLOPPY).
            btn.onClick.AddListener(() => { if (t >= armAfter) locked = true; });

            yield return null;   // never observe the click frame's own key state

            while (!locked && t < total)
            {
                // A paused game must not sweep: the bar runs on unscaled time, so without this the
                // needle would race on behind the pause menu and auto-resolve the attack as SLOPPY.
                if (PauseMenu.IsPaused) { yield return null; continue; }
                t += Time.unscaledDeltaTime;
                pos = t < sweep ? Mathf.Clamp01(t / sweep) : Mathf.Clamp01(1f - (t - sweep) / sweep);
                nrt.anchorMin = new Vector2(pos, 0f); nrt.anchorMax = new Vector2(pos, 1f);
                var kb = Keyboard.current;
                if (t >= armAfter && kb != null && kb.spaceKey.wasPressedThisFrame) locked = true;
                yield return null;
            }

            float off = Mathf.Abs(pos - 0.5f);
            float mult; string call; Color cc;
            if (locked && off < 0.05f) { mult = 1.18f; call = "PERFECT!"; cc = Gold; }
            else if (locked && off < 0.18f) { mult = 1f; call = "GOOD"; cc = Accent; }
            else { mult = 0.9f; call = "SLOPPY"; cc = TxtMuted; }
            GameBootstrap.Instance?.Audio?.PlaySfx(mult > 1.1f ? "crit" : "ui_click");
            juice?.Announce(hero.transform.position + Vector3.up * 2.5f, call, cc, mult > 1.1f ? 44f : 30f);

            ClearChildren(actionPanel);
            controller.SubmitAction(ab, target, mult, stake);
            timingCo = null;
        }

        // --- the stake: how hard do you push the d20? ---------------------------------
        // The SPECIAL used to be a slot machine — press, watch, accept. The stake makes it a read on
        // the board: is the boss one hit from dead, or would a backfire right now lose the run?
        // Each option is annotated with what it costs as well as what it buys, because a stake whose
        // downside is hidden is not a decision.
        private void BuildStakeMenu(Entity hero, Ability ab, Entity target)
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = $"{ab.displayName.ToUpper()} — HOW HARD DO YOU PUSH?";
            float y = 0f;

            var steady = MakeSimpleButton(actionPanel, "»  STEADY  —  can't backfire, smaller payoff", new Vector2(0, -y), Accent, true);
            steady.onClick.AddListener(() => StartTimingBar(hero, ab, target, RiskStake.Steady));
            y += 40f;

            var press = MakeSimpleButton(actionPanel, "»  PRESS  —  the die as written", new Vector2(0, -y), Accent, true);
            press.onClick.AddListener(() => StartTimingBar(hero, ab, target, RiskStake.Press));
            y += 40f;

            var allIn = MakeSimpleButton(actionPanel, "»  ALL IN  —  bigger jackpot, bigger backfire", new Vector2(0, -y), Gold, true);
            allIn.onClick.AddListener(() => StartTimingBar(hero, ab, target, RiskStake.AllIn));
            y += 40f;

            var back = MakeSimpleButton(actionPanel, "←  Back", new Vector2(0, -y), TxtMuted, true);
            back.onClick.AddListener(() => BuildActionMenu(hero));
        }

        // --- overdrive: three answers to three different board states -----------------
        private void BuildOverdriveMenu(Entity hero)
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = "OVERDRIVE — CHOOSE YOUR SPEND";
            var ctx = controller.Context;
            var boss = ctx != null ? ctx.boss : null;
            float y = 0f;

            // Each option is annotated with WHY you would pick it right now, so the choice teaches
            // itself instead of needing a wiki.
            int fallen = 0, wounded = 0;
            if (ctx != null)
                foreach (var h in ctx.heroes)
                {
                    if (h == null) continue;
                    if (!h.IsAlive) fallen++;
                    else if (h.currentHP < h.stats.maxHP * 0.5f) wounded++;
                }
            bool bossCharging = boss != null && boss.telegraphedAbility != null;
            bool canSunder = boss != null && boss.IsAlive && !boss.isStaggered;

            MakeOverdriveOption(hero, ref y, "»  SURGE",
                $"×{(controller.balance != null ? controller.balance.overdriveDamageMult : 1.35f):0.00} party damage for {(controller.balance != null ? controller.balance.overdriveHeroTurns : 3)} hero turns",
                "close out a fight you're already winning", Gold, true, ChargeSystem.OverdriveMode.Surge);

            MakeOverdriveOption(hero, ref y, "»  SUNDER",
                canSunder ? "BREAK the boss instantly — vents Fury, cancels its charge" : "the boss is already broken",
                bossCharging ? "<color=#FFD24A>it is charging RIGHT NOW</color>" : "skip the meter, open the burst window",
                Accent, canSunder, ChargeSystem.OverdriveMode.Sunder);

            MakeOverdriveOption(hero, ref y, "✚  RALLY",
                $"heal the party {(controller.balance != null ? controller.balance.overdriveRallyHealPercent : 45)}% and REVIVE the fallen",
                fallen > 0 ? $"<color=#FF7A6B>{fallen} hero down</color>" : wounded > 0 ? $"{wounded} badly wounded" : "save it — nobody needs it yet",
                HpGreen, true, ChargeSystem.OverdriveMode.Rally);

            var back = MakeSimpleButton(actionPanel, "←  Back to skills", new Vector2(0, -y), Accent, true);
            back.onClick.AddListener(() => BuildActionMenu(hero));
        }

        private void MakeOverdriveOption(Entity hero, ref float y, string title, string effect, string hint, Color accent, bool enabled, ChargeSystem.OverdriveMode mode)
        {
            var go = new GameObject("Overdrive"); go.transform.SetParent(actionPanel, false);
            var img = go.AddComponent<Image>(); Soft(img);
            img.color = enabled ? new Color(accent.r * 0.26f, accent.g * 0.26f, accent.b * 0.26f, 0.95f)
                                : new Color(0.12f, 0.13f, 0.16f, 0.8f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, 58);
            rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = enabled;
            var cb = btn.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f); cb.fadeDuration = 0.07f; btn.colors = cb;

            var t1 = MakeText(rt, title, fontHeader, 16, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(t1, 14, 36, 12, 4); t1.color = enabled ? accent : TxtMuted;
            t1.enableWordWrapping = false; t1.overflowMode = TextOverflowModes.Overflow;
            var t2 = MakeText(rt, effect, fontBody, 12, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(t2, 14, 18, 12, 24); t2.color = TxtMain; t2.richText = true;
            var t3 = MakeText(rt, hint, fontBody, 11, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(t3, 14, 3, 12, 40); t3.color = TxtMuted; t3.richText = true;

            if (enabled) btn.onClick.AddListener(() =>
            {
                GameBootstrap.Instance?.Audio?.PlaySfx("crit");
                controller.SubmitOverdrive(mode);
                BuildActionMenu(hero);
            });
            y += 62f;
        }

        // --- target picker ------------------------------------------------------------
        // Only single-enemy skills need a choice, and only while the boss actually has living adds.
        // THE SINGLE FUNNEL from "the target is settled" to resolution, so every path into an action
        // agrees on which action-commands that skill earns.
        //
        // It did not, and the bug was invisible: the target picker called StartTimingBar directly,
        // and that 3-arg overload hardcodes RiskStake.Press. So whenever the boss had a living add —
        // i.e. most of the Dragon fight, which opens with two whelps — picking a SPECIAL skipped the
        // STAKE menu entirely and silently resolved at PRESS. The player never saw the choice and
        // never knew it existed.
        private void BeginAction(Entity hero, Ability ab, Entity target)
        {
            // A risk-die SPECIAL asks how hard to push BEFORE the strike is timed.
            if (ab.rollsRiskDie && IsDamaging(ab)) { BuildStakeMenu(hero, ab, target); return; }
            if (IsDamaging(ab)) { StartTimingBar(hero, ab, target); return; }
            controller.SubmitAction(ab, target);
        }

        private bool NeedsTargetChoice(Ability ab)
        {
            if (ab == null || controller == null || controller.Context == null) return false;
            if (ab.targetRule != TargetRule.SingleEnemy) return false;
            int alive = 0;
            foreach (var m in controller.Context.minions) if (m != null && m.IsAlive) alive++;
            return alive > 0 && controller.Context.boss != null && controller.Context.boss.IsAlive;
        }

        private void BuildTargetPicker(Entity hero, Ability ab)
            => BuildEnemyPicker(hero, $"{ab.displayName.ToUpper()} — PICK A TARGET", ab,
                                t => BeginAction(hero, ab, t), () => BuildActionMenu(hero));

        private void BuildItemTargetPicker(Entity hero, ItemDefinition item)
            => BuildEnemyPicker(hero, $"{item.displayName.ToUpper()} — PICK A TARGET", item.ability,
                                t => controller.SubmitItem(item, t), () => BuildItemMenu(hero));

        // One row per living enemy with its HP and (for the boss) its Break progress, so choosing
        // "finish the whelp" vs "keep breaking the dragon" is an informed decision. Shared by skills
        // and consumables — `previewAbility` only drives the combo tag, `onPick` does the committing.
        private void BuildEnemyPicker(Entity hero, string title, Ability previewAbility,
                                      System.Action<Entity> onPick, System.Action onBack)
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = title;
            var ctx = controller.Context;
            float y = 0f;
            var options = new List<Entity>();
            foreach (var m in ctx.minions) if (m != null && m.IsAlive) options.Add(m);
            if (ctx.boss != null && ctx.boss.IsAlive) options.Add(ctx.boss);

            foreach (var opt in options)
            {
                var captured = opt;
                var go = new GameObject("Target"); go.transform.SetParent(actionPanel, false);
                var img = go.AddComponent<Image>(); Soft(img);
                img.color = captured.isBoss ? new Color(0.30f, 0.12f, 0.13f, 0.95f) : new Color(0.17f, 0.20f, 0.28f, 0.95f);
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, 46);
                rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
                var b = go.AddComponent<Button>(); b.targetGraphic = img;
                var cb2 = b.colors; cb2.highlightedColor = new Color(1.3f, 1.3f, 1.3f); cb2.fadeDuration = 0.07f; b.colors = cb2;

                var nameT = MakeText(rt, captured.displayName, fontHeader, 16, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
                Stretch(nameT, 14, 22, 12, 3); nameT.enableWordWrapping = false; nameT.overflowMode = TextOverflowModes.Overflow;
                nameT.color = captured.isBoss ? new Color(1f, 0.62f, 0.52f) : TxtMain;

                string detail = $"HP {captured.currentHP}/{captured.stats.maxHP}";
                if (captured.isBoss)
                    detail += captured.isStaggered ? "   <color=#FFD24A>BROKEN — burst now!</color>"
                            : $"   break {Mathf.RoundToInt(captured.staggerMeter)}/{Mathf.RoundToInt(captured.staggerThreshold)}";
                if (previewAbility != null)
                    detail += ComboTag(hero, previewAbility,
                                       previewAbility.followsAttunement ? hero.currentAttunement : previewAbility.element,
                                       captured);
                var dT = MakeText(rt, detail, fontBody, 12, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
                Stretch(dT, 14, 3, 12, 24); dT.color = TxtMuted; dT.richText = true;

                b.onClick.AddListener(() =>
                {
                    GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                    onPick(captured);
                });
                y += 50f;
            }
            var back = MakeSimpleButton(actionPanel, "←  Back", new Vector2(0, -y), Accent, true);
            back.onClick.AddListener(() => onBack());
        }

        // --- action menu --------------------------------------------------------------
        private void BuildActionMenu(Entity hero)
        {
            ClearChildren(actionPanel);
            var ctx = controller.Context;
            if (menuTitle != null) menuTitle.text = $"{hero.displayName.ToUpper()} — CHOOSE A SKILL";
            float y = 0f;
            const float row = 50f;
            foreach (var ability in hero.abilities)
            {
                var ab = ability;
                bool affordable = hero.currentMP >= ab.mpCost && !hero.IsOnCooldown(ab);
                MakeAbilityButton(actionPanel, hero, ab, ctx, new Vector2(0, -y), affordable);
                y += row;
            }
            var charge = ctx != null ? ctx.charge : null;
            if (charge != null && charge.IsFull)
            {
                var od = MakeSimpleButton(actionPanel, "»  OVERDRIVE — spend the meter", new Vector2(0, -y), Gold, true);
                od.onClick.AddListener(() => BuildOverdriveMenu(hero));
                y += 38f;
            }
            var move = MakeSimpleButton(actionPanel, $"↕  Move to {(hero.backRow ? "FRONT" : "BACK")} row", new Vector2(0, -y), Accent, true);
            move.onClick.AddListener(() => controller.SubmitReposition());
            y += 38f;

            // Back out to the hero pick without spending the turn. HOLD is gone — choosing who acts
            // next IS the ordering mechanic now, so "act after your allies" is just picking them later.
            if (controller.HeroesYetToAct != null && controller.HeroesYetToAct.Count > 1)
            {
                var switchBtn = MakeSimpleButton(actionPanel, "←  Switch hero", new Vector2(0, -y), TxtMuted, true);
                switchBtn.onClick.AddListener(() =>
                {
                    GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                    controller.CancelHeroSelection();
                });
                y += 38f;
            }

            // Consumables: one button into the item submenu (hidden while the satchel is empty).
            var run = GameBootstrap.Instance?.Run;
            int owned = run != null ? run.inventory.Count : 0;
            if (owned > 0 && controller.itemCatalog != null && controller.itemCatalog.Count > 0)
            {
                var items = MakeSimpleButton(actionPanel, $"»  Items  ({owned})", new Vector2(0, -y), new Color(0.72f, 0.55f, 0.95f), true);
                items.onClick.AddListener(() => BuildItemMenu(hero));
            }
        }

        // The item submenu: one two-line card per DISTINCT owned item (name ×count / description),
        // plus a back row. Using one routes through BattleController.SubmitItem — same turn spend,
        // same presentation pipeline as a skill.
        private void BuildItemMenu(Entity hero)
        {
            ClearChildren(actionPanel);
            if (menuTitle != null) menuTitle.text = $"{hero.displayName.ToUpper()} — USE AN ITEM";
            var run = GameBootstrap.Instance?.Run;
            float y = 0f;
            if (run != null)
            {
                var seen = new HashSet<string>();
                foreach (var itemName in run.inventory)
                {
                    if (!seen.Add(itemName)) continue;
                    var def = controller.itemCatalog.Find(d => d != null && d.name == itemName);
                    if (def == null || def.ability == null) continue;
                    int count = run.CountItem(itemName);

                    var go = new GameObject("Item"); go.transform.SetParent(actionPanel, false);
                    var img = go.AddComponent<Image>(); Soft(img);
                    img.color = new Color(0.17f, 0.13f, 0.26f, 0.95f);
                    var rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(0, 46);
                    rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
                    var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
                    var cb = btn.colors; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.fadeDuration = 0.07f; btn.colors = cb;

                    var nameT = MakeText(rt, $"{def.displayName}  <color=#B9A3E8>×{count}</color>", fontHeader, 16, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
                    Stretch(nameT, 14, 22, 12, 3); nameT.enableWordWrapping = false; nameT.overflowMode = TextOverflowModes.Overflow;
                    var descT = MakeText(rt, def.description, fontBody, 12, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
                    Stretch(descT, 14, 3, 12, 24); descT.color = TxtMuted;

                    var captured = def;
                    btn.onClick.AddListener(() =>
                    {
                        GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                        // Offensive consumables need the same target choice a skill gets. Without it
                        // PickTarget's adds-first rule force-fed every Slick Flask and Flashbang to
                        // the first living whelp — a Wet coating on a 90 HP add that dies before you
                        // can detonate it, which is the entire value of the item thrown away.
                        if (NeedsTargetChoice(captured.ability)) BuildItemTargetPicker(hero, captured);
                        else controller.SubmitItem(captured, PickTarget(hero, captured.ability));
                    });
                    y += 50f;
                }
            }
            var back = MakeSimpleButton(actionPanel, "←  Back to skills", new Vector2(0, -y), Accent, true);
            back.onClick.AddListener(() => BuildActionMenu(hero));
        }

        private void MakeAbilityButton(RectTransform parent, Entity hero, Ability ab, BattleContext ctx, Vector2 pos, bool affordable)
        {
            var go = new GameObject("Ability"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); Soft(img);
            img.color = affordable ? new Color(0.13f, 0.18f, 0.27f, 0.95f) : new Color(0.12f, 0.13f, 0.16f, 0.80f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(0, 46);
            rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);

            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = affordable;
            var cb = btn.colors; cb.normalColor = Color.white; cb.disabledColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.pressedColor = new Color(0.8f, 0.85f, 0.95f);
            cb.fadeDuration = 0.07f; btn.colors = cb;

            // accent edge: risky specials glow gold, everything else teal
            var edge = new GameObject("Edge"); edge.transform.SetParent(rt, false);
            var eimg = edge.AddComponent<Image>(); Soft(eimg);
            eimg.color = !affordable ? new Color(0.4f, 0.4f, 0.4f, 0.5f) : ab.rollsRiskDie ? Gold : Accent;
            var ert = eimg.rectTransform; ert.anchorMin = new Vector2(0, 0); ert.anchorMax = new Vector2(0, 1);
            ert.pivot = new Vector2(0, 0.5f); ert.sizeDelta = new Vector2(4, 0); ert.anchoredPosition = new Vector2(0, 0);

            float textLeft = 14f;
            if (ab.icon != null)
            {
                var ig = new GameObject("Icon"); ig.transform.SetParent(rt, false);
                var iimg = ig.AddComponent<Image>(); iimg.sprite = ab.icon; iimg.preserveAspect = true;
                if (!affordable) iimg.color = new Color(1, 1, 1, 0.4f);
                var irt = iimg.rectTransform; irt.anchorMin = new Vector2(0, 0.5f); irt.anchorMax = new Vector2(0, 0.5f);
                irt.pivot = new Vector2(0, 0.5f); irt.anchoredPosition = new Vector2(10, 0); irt.sizeDelta = new Vector2(30, 30);
                textLeft = 46f;
            }

            // line 1: name (left) + cost/cooldown (right)
            var name = MakeText(rt, ab.displayName, fontHeader, 16, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(name, textLeft, 22, 86, 3);
            name.color = affordable ? TxtMain : TxtMuted;
            name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Overflow;

            string costStr = ab.cooldown > 0 && hero.IsOnCooldown(ab) ? $"CD {hero.CooldownRemaining(ab)}"
                           : ab.mpCost > 0 ? $"{ab.mpCost} MP" : "free";
            var cost = MakeText(rt, costStr, fontBody, 13, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.zero);
            Stretch(cost, 0, 22, 12, 3);
            cost.color = !affordable ? Danger : ab.mpCost > 0 ? MpBlue : TxtMuted;

            // line 2: hit% · damage band · reaction tag · combo-ready callout
            string preview = BuildPreview(hero, ab, ctx);
            var detail = MakeText(rt, preview, fontBody, 12, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(detail, textLeft, 3, 12, 24);
            detail.color = TxtMuted;
            detail.richText = true;
            // a combo would fire from this button RIGHT NOW — flag the whole card gold
            if (affordable && preview.Contains(">>")) eimg.color = Gold;

            if (affordable) btn.onClick.AddListener(() =>
            {
                GameBootstrap.Instance?.Audio?.PlaySfx("ui_click");
                // More than one legal enemy (the boss plus living adds)? Let the player CHOOSE —
                // otherwise the whole minion layer is decided for you and adds-first is a straitjacket.
                if (NeedsTargetChoice(ab))
                {
                    BuildTargetPicker(hero, ab);
                    return;
                }
                BeginAction(hero, ab, PickTarget(hero, ab));
            });
        }

        private Button MakeSimpleButton(RectTransform parent, string label, Vector2 pos, Color accent, bool enabled)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); Soft(img);
            img.color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.92f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(0, 34);
            rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = enabled;
            var cb = btn.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f); cb.fadeDuration = 0.07f; btn.colors = cb;
            var t = MakeText(rt, label, fontBody, 15, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(t, 0, 0, 0, 0); t.color = accent;
            if (enabled) btn.onClick.AddListener(() => GameBootstrap.Instance?.Audio?.PlaySfx("ui_click"));
            return btn;
        }

        // --- helpers ------------------------------------------------------------------
        private static bool IsDamaging(Ability a) => a.effectType == EffectType.Attack || a.effectType == EffectType.MultiHit;

        private string BuildPreview(Entity hero, Ability ab, BattleContext ctx)
        {
            if (!IsDamaging(ab))
                return ab.targetRule == TargetRule.SingleAlly || ab.targetRule == TargetRule.Self
                    ? "<color=#5FD17A>support</color>" : "<color=#9AA3B5>utility</color>";
            if (ctx.boss == null || ctx.damage == null) return "";
            ElementType el = ab.followsAttunement ? hero.currentAttunement : ab.element;
            var info = new DamageInfo
            {
                source = hero, target = ctx.boss, ability = ab, element = el,
                basePower = ab.power, isMagic = ab.isMagic, forceHit = true, hitTier = ab.hitTier
            };
            var pv = ctx.damage.PreviewDamage(info);
            int hitPct = Mathf.RoundToInt(EstimateHit(hero, ab, ctx.boss) * 100);
            bool revealed = weaknessSeen || ctx.weaknessRevealed;
            string tag = pv.absorb ? "  <color=#FF6B6B>ABSORB!</color>"
                       : revealed && pv.reaction == ElementReaction.Weak ? "  <color=#46C8E6>WEAK</color>"
                       : revealed && pv.reaction == ElementReaction.Resist ? "  <color=#8A95A8>resist</color>"
                       : revealed && pv.reaction == ElementReaction.Immune ? "  <color=#8A95A8>immune</color>" : "";
            string dmg = pv.absorb ? "<color=#5FD17A>heals!</color>" : ab.hits > 1 ? $"{pv.min}-{pv.max}×{ab.hits}" : $"{pv.min}-{pv.max}";
            string risk = ab.rollsRiskDie ? "  <color=#F2C14E>d20!</color>" : "";
            string hitCol = hitPct >= 85 ? "#7FD08A" : hitPct >= 70 ? "#E6C84A" : "#E08A6B";
            return $"<color={hitCol}>{hitPct}%</color>  <color=#C2C8D4>{dmg}</color>{tag}{ComboTag(hero, ab, el)}{risk}";
        }

        // COMBO-READY callouts: when the default target carries a setup status this ability would
        // detonate, SAY SO on the button — the synergy web becomes a visible plan, not a secret.
        private string ComboTag(Entity hero, Ability ab, ElementType el) => ComboTag(hero, ab, el, null);

        private string ComboTag(Entity hero, Ability ab, ElementType el, Entity explicitTarget)
        {
            var tgt0 = explicitTarget != null ? explicitTarget : PickTarget(hero, ab);
            // FEEDING WARNING: against a devourer, laying another setup is a trap — it heals and
            // enrages him. Say so on the button, in red, BEFORE the player commits the turn.
            if (tgt0 != null && tgt0.team == Team.Enemies && ab.statusesToApply != null
                && tgt0.Brain is RPGArena.Combat.AI.DevourerAI dv
                && RPGArena.Combat.AI.DevourerAI.DevourableCount(tgt0) >= 1)
            {
                foreach (var s in ab.statusesToApply)
                {
                    if (s == null) continue;
                    if (s.flag == StatusFlag.Wet || s.flag == StatusFlag.Oiled || s.flag == StatusFlag.Marked)
                        return "  <color=#FF6B5C>>>FEEDS HIM!</color>";
                }
            }

            if (!IsDamaging(ab)) return "";
            var tgt = tgt0;
            if (tgt == null || tgt.team == Team.Heroes) return "";
            var st = tgt.Status;
            bool phys = el == ElementType.Physical;
            if (st.Has(StatusFlag.Frozen) && phys && st.Has(StatusFlag.Marked)) return "  <color=#FFD24A>>>SHATTER+BRITTLE!</color>";
            if (st.Has(StatusFlag.Frozen) && phys) return "  <color=#FFD24A>>>SHATTER ×2.3!</color>";
            if (st.Has(StatusFlag.Marked) && st.Has(StatusFlag.Oiled) && phys) return "  <color=#FFB347>>>QUARRY ×1.9!</color>";
            if (st.Has(StatusFlag.Wet) && el == ElementType.Ice) return "  <color=#7FE3FF>>>FREEZE!</color>";
            if (st.Has(StatusFlag.Oiled) && el == ElementType.Fire) return "  <color=#FFA24A>>>IGNITE!</color>";
            if (st.Has(StatusFlag.Wet) && phys) return "  <color=#9BD1FF>>>soaked +dmg</color>";
            if (st.Has(StatusFlag.Oiled) && phys) return "  <color=#FFCF9B>>>slick +dmg</color>";
            if (st.Has(StatusFlag.Marked) && phys) return "  <color=#E8B9FF>>>marked</color>";
            return "";
        }

        private float EstimateHit(Entity hero, Ability a, Entity boss)
        {
            if (a.autoHit || a.hitTier == HitTier.Reliable) return 1f;
            var cfg = controller.balance;
            float tier = a.hitTier == HitTier.Risky ? cfg.riskyHitBase : cfg.standardHitBase;
            float eva = boss != null && !boss.isStaggered ? boss.Evasion : 0f;
            return Mathf.Clamp(tier + (hero.Accuracy - eva) * cfg.accuracyToPercent, cfg.hitFloor, cfg.hitCeiling);
        }

        private Entity PickTarget(Entity hero, Ability a)
        {
            var ctx = controller.Context;
            switch (a.targetRule)
            {
                case TargetRule.SingleAlly: return TargetingSystem.LowestHP(ctx.heroes);
                case TargetRule.Self: return hero;
                default:
                    // Adds-first (mirrors BattleController.ResolveTargets): clear the skirmish line,
                    // then the boss.
                    foreach (var m in ctx.minions) if (m != null && m.IsAlive) return m;
                    return ctx.boss;
            }
        }

        private void AddLog(string line, Color color)
        {
            logEntries.Add(new LogEntry { text = line, color = color });
            while (logEntries.Count > 5) logEntries.RemoveAt(0);
            if (log) log.text = ComposeLog();
            if (logPanel != null && !logPanel.activeSelf) logPanel.SetActive(true);
        }

        private string ComposeLog()
        {
            var sb = new System.Text.StringBuilder();
            int n = logEntries.Count;
            for (int i = 0; i < n; i++)
            {
                float a = n <= 1 ? 1f : Mathf.Lerp(0.35f, 1f, (float)i / (n - 1));
                var c = logEntries[i].color; c.a = a;
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(c)).Append('>')
                  .Append(logEntries[i].text).Append("</color>");
                if (i < n - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private readonly HashSet<Image> barInitialised = new();

        private void SetFill(Image img, float cur, float max)
        {
            if (!img) return;
            float v = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
            targetFill[img] = v;
            // Bars are built at fillAmount 1. A meter that STARTS empty (Break, Valor) would other-
            // wise spend its first seconds visibly draining from full, complete with the white
            // chip-away ghost — which reads as a glitch. Snap both to the true value the first time.
            if (barInitialised.Add(img))
            {
                img.fillAmount = v;
                if (ghostOf.TryGetValue(img, out var g) && g != null) g.fillAmount = v;
            }
        }

        private void AnimateBars()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var kv in targetFill)
            {
                var fill = kv.Key; if (fill == null) continue;
                float t = kv.Value;
                fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, t, dt * 1.7f);
                if (ghostOf.TryGetValue(fill, out var ghost) && ghost != null)
                {
                    if (ghost.fillAmount > t) ghost.fillAmount = Mathf.MoveTowards(ghost.fillAmount, t, dt * 0.55f);
                    else ghost.fillAmount = fill.fillAmount;
                }
            }
        }

        private static string FormatWeakness(Entity boss)
        {
            if (boss == null || boss.elementProfile == null) return "";
            string weak = Join(boss.elementProfile.weakTo);
            string absorb = Join(boss.elementProfile.absorbs);
            string s = "";
            if (weak.Length > 0) s += $"<color=#46C8E6>Weak: {weak}</color>";
            if (absorb.Length > 0) s += (s.Length > 0 ? "      " : "") + $"<color=#FF6B6B>Absorbs: {absorb}</color>";
            return s;
        }

        private static string Join(ElementType[] arr) =>
            arr == null || arr.Length == 0 ? "" : string.Join("/", arr);

        // ============================ UI construction ================================
        private void BuildUI()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("BattleHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // ~15% LARGER than the other canvases' 1920x1080 on purpose: the battle HUD is the one
            // screen where the player reads numbers under time pressure, and playtest feedback was
            // that it ran small. A smaller reference resolution scales every element up uniformly.
            scaler.referenceResolution = new Vector2(1664, 936);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasGo;
            var root = canvasGo.GetComponent<RectTransform>();

            // ---- BOSS (top center) ----
            MakePanel(root, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(720, 116), Panel);
            bossName = MakeText(root, "Boss", fontHeader, 30, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(bossName, new Vector2(0, -30), new Vector2(680, 38)); bossName.color = new Color(1f, 0.62f, 0.52f);
            bossHpFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(672, 24), Danger);
            bossHpText = MakeText(root, "HP", fontBody, 14, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(bossHpText, new Vector2(0, -62), new Vector2(672, 24));
            bossStaggerFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(672, 11), Gold);
            bossStaggerText = MakeText(root, "BREAK", fontBody, 10, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(bossStaggerText, new Vector2(0, -88), new Vector2(672, 12)); bossStaggerText.color = new Color(0.1f, 0.08f, 0f);
            bossWeakness = MakeText(root, "", fontBody, 14, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(bossWeakness, new Vector2(0, -104), new Vector2(700, 20));
            // boss status badges
            bossStatusRow = MakeRow(root, new Vector2(0.5f, 1f), new Vector2(0, -126), new Vector2(720, 26));

            // Searing Fury pill (top-right of the boss panel): the escalating-damage warning. Hidden at
            // 0 stacks; glows hotter as it climbs so "BREAK it to vent" reads at a glance.
            bossFuryPanel = MakePanel(root, new Vector2(0.5f, 1f), new Vector2(214, -18), new Vector2(270, 26), new Color(0.35f, 0.10f, 0.04f, 0.92f));
            bossFuryText = MakeText(bossFuryPanel.GetComponent<RectTransform>(), "", fontHeader, 13, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(bossFuryText, 6, 2, 6, 2); bossFuryText.color = new Color(1f, 0.6f, 0.3f);
            bossFuryPanel.SetActive(false);

            // telegraph banner (rarely shown — boss attacks every turn now, but kept for the charged variant)
            telegraphPanel = MakePanel(root, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(760, 38), new Color(0.55f, 0.06f, 0.06f, 0.94f));
            telegraph = MakeText(root, "", fontHeader, 20, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Place(telegraph, new Vector2(0, -150), new Vector2(900, 36)); telegraph.color = new Color(1f, 0.93f, 0.4f);
            telegraphPanel.SetActive(false);

            // ---- TURN ORDER (top right) ----
            // taller: enemy rows now carry an INTENT line under the name
            var toPanel = MakePanel(root, new Vector2(1f, 1f), new Vector2(-12, -12), new Vector2(184, 292), Panel).GetComponent<RectTransform>();
            var toTitle = PanelTitle(toPanel, "TURN ORDER", 14, 8, 20); toTitle.color = Accent; toTitle.characterSpacing = 6;
            turnOrderRow = MakeRow(root, new Vector2(1f, 1f), new Vector2(-104, -44), new Vector2(172, 244));

            // ---- PARTY (bottom left): 3 hero cards ----
            for (int i = 0; i < 3; i++)
            {
                var card = MakePanel(root, new Vector2(0f, 0f), new Vector2(16, 16 + i * 96), new Vector2(366, 88), Panel);
                partyCardBg.Add(card.GetComponent<Image>());
                var st = card.GetComponent<RectTransform>();
                // active stripe (left edge)
                var stripe = new GameObject("Stripe"); stripe.transform.SetParent(st, false);
                var simg = stripe.AddComponent<Image>(); Soft(simg); simg.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
                var srt = simg.rectTransform; srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0, 0.5f);
                srt.sizeDelta = new Vector2(4, -8); srt.anchoredPosition = new Vector2(4, 0);
                partyActiveStripe.Add(simg);

                partyName.Add(MakeText(st, "Hero", fontHeader, 18, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, 1f)));
                Place(partyName[i], new Vector2(16, -6), new Vector2(300, 24));
                // HP
                partyHpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(16, -34), new Vector2(338, 16), HpGreen));
                partyHpText.Add(MakeText(st, "", fontBody, 11, TextAlignmentOptions.MidlineRight, new Vector2(0f, 1f), new Vector2(0f, 1f)));
                Place(partyHpText[i], new Vector2(16, -34), new Vector2(330, 16)); partyHpText[i].color = Color.white;
                // MP
                partyMpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(16, -54), new Vector2(338, 12), MpBlue));
                partyMpText.Add(MakeText(st, "", fontBody, 10, TextAlignmentOptions.MidlineRight, new Vector2(0f, 1f), new Vector2(0f, 1f)));
                Place(partyMpText[i], new Vector2(16, -54), new Vector2(330, 12)); partyMpText[i].color = new Color(0.85f, 0.9f, 1f);
                heroStatusRows.Add(MakeRow(st, new Vector2(0f, 0f), new Vector2(16, 6), new Vector2(330, 16)));
            }

            // ---- ACTION MENU (bottom right) ---- (410 tall: 5 skill rows + Overdrive + Move +
            // Items all fit with margin — smaller sizes used to clip the last row)
            menuPanelGo = MakePanel(root, new Vector2(1f, 0f), new Vector2(-16, 16), new Vector2(424, 410), Panel);
            var menuPanel = menuPanelGo.GetComponent<RectTransform>();
            menuPanelGo.SetActive(false);   // shown only while a hero is choosing
            menuTitle = PanelTitle(menuPanel, "", 14, 8, 20); menuTitle.color = Accent; menuTitle.characterSpacing = 3;
            var menuInner = MakeRow(menuPanel, new Vector2(0f, 1f), new Vector2(10, -34), new Vector2(404, 370));
            menuInner.anchorMin = new Vector2(0f, 1f); menuInner.anchorMax = new Vector2(1f, 1f);
            menuInner.offsetMin = new Vector2(10, -404); menuInner.offsetMax = new Vector2(-10, -34);
            menuInner.pivot = new Vector2(0.5f, 1f);
            actionPanel = menuInner;

            // coach hint (above the menu, first input only)
            coachPanel = MakePanel(root, new Vector2(1f, 0f), new Vector2(-16, 434), new Vector2(424, 40), new Color(0.10f, 0.13f, 0.05f, 0.9f));
            coachCaption = MakeText(coachPanel.GetComponent<RectTransform>(),
                                    "Pick a skill:  <color=#7FD08A>%</color> = hit chance · the band = damage · <color=#46C8E6>WEAK</color> is good · <color=#F2C14E>d20!</color> = a risky gamble",
                                    fontBody, 12.5f, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(coachCaption, 10, 4, 10, 4); coachCaption.color = new Color(1f, 0.95f, 0.7f);
            coachPanel.SetActive(false);

            // ---- BRACE prompt (above the valor bar; hidden until an enemy winds up) ----
            bracePanel = MakePanel(root, new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(380, 56), new Color(0.32f, 0.06f, 0.05f, 0.95f));
            braceImg = bracePanel.GetComponent<Image>();
            braceText = MakeText(bracePanel.GetComponent<RectTransform>(), "", fontHeader, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(braceText, 8, 4, 8, 4);
            bracePanel.SetActive(false);

            // ---- FOLLOW-UP prompt (flashes above the brace slot on a random beat) ----
            followPanel = MakePanel(root, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(340, 48), new Color(0.30f, 0.22f, 0.04f, 0.96f));
            followText = MakeText(followPanel.GetComponent<RectTransform>(), "", fontHeader, 22, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(followText, 8, 4, 8, 4); followText.color = Gold;
            followPanel.SetActive(false);

            // ---- VALOR (bottom center) ----
            MakePanel(root, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(456, 28), Panel);
            valorFill = MakeBar(root, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(448, 24), Gold);
            valorText = MakeText(root, "VALOR", fontHeader, 13, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            Place(valorText, new Vector2(0, 14), new Vector2(448, 24)); valorText.color = new Color(0.1f, 0.08f, 0f);

            // ---- LOG (docked above the party cards; hidden until the first entry so an empty box
            // never sits on the battlefield) ----
            logPanel = MakePanel(root, new Vector2(0f, 0f), new Vector2(16, 314), new Vector2(340, 128), Panel);
            log = MakeText(logPanel.GetComponent<RectTransform>(), "", fontBody, 15, TextAlignmentOptions.BottomLeft, Vector2.zero, Vector2.zero);
            Stretch(log, 12, 8, 12, 8); log.richText = true; log.lineSpacing = 6f;
            logPanel.SetActive(false);

            // ---- ANNOUNCER (centre screen): "Warrior used Power Strike on The Dragon" ----
            var annGo = new GameObject("Announcer"); annGo.transform.SetParent(root, false);
            announceBg = annGo.AddComponent<Image>(); Soft(announceBg);
            announceBg.color = new Color(0.02f, 0.03f, 0.06f, 0.72f); announceBg.raycastTarget = false;
            var annRt = announceBg.rectTransform;
            annRt.anchorMin = annRt.anchorMax = new Vector2(0.5f, 0.72f); annRt.pivot = new Vector2(0.5f, 0.5f);
            annRt.anchoredPosition = Vector2.zero; annRt.sizeDelta = new Vector2(760, 52);
            announceText = MakeText(annRt, "", fontHeader, 26, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Stretch(announceText, 12, 4, 12, 4);
            annGo.SetActive(false);

            // ---- COMBO ADVISOR (top left): live, board-driven suggestions ----
            advisorPanel = MakePanel(root, new Vector2(0f, 1f), new Vector2(12, -12), new Vector2(348, 148), Panel);
            var advRt = advisorPanel.GetComponent<RectTransform>();
            var advTitle = PanelTitle(advRt, "COMBO ADVISOR", 14, 8, 20); advTitle.color = Gold; advTitle.characterSpacing = 6;
            advisorText = MakeText(advRt, "", fontBody, 14.5f, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.zero);
            Stretch(advisorText, 14, 8, 10, 34); advisorText.richText = true; advisorText.lineSpacing = 10f;
        }

        // --- combo advisor ----------------------------------------------------------------
        // Reads the actual board every frame and says what is WORTH DOING RIGHT NOW. This is the
        // "occupy the player from minute one" panel: it teaches the combo web by pointing at the
        // live opportunity instead of explaining theory.
        private GameObject advisorPanel;
        private TMP_Text advisorText;
        private string advisorSig = "";

        private void RefreshAdvisor()
        {
            if (advisorText == null || controller == null || controller.Context == null) return;
            var ctx = controller.Context;
            var boss = ctx.boss;
            if (boss == null || !boss.IsAlive) { if (advisorPanel.activeSelf) advisorPanel.SetActive(false); return; }
            if (!advisorPanel.activeSelf) advisorPanel.SetActive(true);

            var lines = new List<string>(4);
            var st = boss.Status;
            bool wet = st != null && st.Has(StatusFlag.Wet);
            bool oiled = st != null && st.Has(StatusFlag.Oiled);
            bool marked = st != null && st.Has(StatusFlag.Marked);
            bool frozen = st != null && st.Has(StatusFlag.Frozen);

            if (frozen)
                lines.Add("<color=#F2C14E>» FROZEN — hit it with PHYSICAL for SHATTER ×2.3!</color>");
            else if (wet)
                lines.Add("<color=#46C8E6>» it is WET — Ice now = guaranteed FREEZE</color>");
            else if (marked && oiled)
                lines.Add("<color=#F2C14E>» MARKED + OILED — any physical hit = QUARRY ×1.9</color>");
            else if (oiled)
                lines.Add("<color=#8FD8A0>» OILED — physical hits +15%; add a MARK for QUARRY</color>");
            else
                lines.Add("<color=#8FD8A0>» coat it first: Water Bomb (Thief) or Pitch Arrow (Archer)</color>");

            if (boss.telegraphedAbility != null)
                lines.Add("<color=#FF7A5C>» it is CHARGING — hits build +50% Break now. Race it!</color>");
            else if (boss.isStaggered)
                lines.Add("<color=#F2C14E>» BROKEN — everything ×1.85. Unload your biggest hits!</color>");
            else if (boss.rageStacks >= 4)
                lines.Add($"<color=#FF9E7A>» Fury ×{boss.rageStacks} and climbing — BREAK it to vent</color>");

            if (ctx.charge != null && ctx.charge.IsFull)
                lines.Add("<color=#F2C14E>» VALOR FULL — Overdrive is ready</color>");
            else if (marked && frozen)
                lines.Add("<color=#B9A3E8>» BRITTLE (Marked+Frozen) — crits almost guaranteed</color>");

            string text = string.Join("\n", lines);
            if (text == advisorSig) return;
            advisorSig = text;
            advisorText.text = text;
        }

        // Position a TMP text by anchored pos + size (anchor/pivot already set by MakeText).
        private static void Place(TMP_Text t, Vector2 pos, Vector2 size)
        {
            var rt = t.rectTransform; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        // Stretch a TMP text to fill its parent with (left, bottom, right, top) insets — used for
        // labels INSIDE a button/badge rect (MakeText's point-anchor doesn't honour offsets).
        private static void Stretch(TMP_Text t, float left, float bottom, float right, float top)
        {
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        // A title bar pinned to the TOP of a panel (full width), so it never drifts off the panel.
        private TMP_Text PanelTitle(RectTransform panel, string text, float size, float topInset, float height)
        {
            var t = MakeText(panel, text, fontHeader, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(8, -(topInset + height)); rt.offsetMax = new Vector2(-8, -topInset);
            return t;
        }

        private TMP_Text MakeText(RectTransform parent, string content, TMP_FontAsset fontAsset, float size,
                                  TextAlignmentOptions align, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            t.text = content; t.fontSize = size; t.alignment = align; t.color = TxtMain;
            t.richText = true; t.raycastTarget = false;
            t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            return t;
        }

        private Image MakeBar(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var bg = new GameObject("BarBG"); bg.transform.SetParent(parent, false);
            var bgImg = bg.AddComponent<Image>(); bgImg.color = Track; Soft(bgImg);
            var brt = bgImg.rectTransform; brt.anchorMin = brt.anchorMax = anchor; brt.pivot = anchor; brt.anchoredPosition = pos; brt.sizeDelta = size;
            var gh = new GameObject("Ghost"); gh.transform.SetParent(bg.transform, false);
            var ghImg = gh.AddComponent<Image>(); ghImg.color = new Color(1f, 1f, 1f, 0.5f); ghImg.sprite = RoundedSprite(); ghImg.type = Image.Type.Filled; ghImg.fillMethod = Image.FillMethod.Horizontal; ghImg.fillOrigin = 0; ghImg.fillAmount = 1f;
            var grt = ghImg.rectTransform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = new Vector2(2, 2); grt.offsetMax = new Vector2(-2, -2);
            var fg = new GameObject("Fill"); fg.transform.SetParent(bg.transform, false);
            var img = fg.AddComponent<Image>(); img.color = color; img.sprite = RoundedSprite(); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f;
            var frt = img.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            ghostOf[img] = ghImg; targetFill[img] = 1f;
            return img;
        }

        private GameObject MakePanel(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color; Soft(img);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            // hairline stroke for a crisper framed edge
            var edge = new GameObject("Edge"); edge.transform.SetParent(rt, false);
            var eimg = edge.AddComponent<Image>(); eimg.color = Stroke; eimg.sprite = RoundedSprite(); eimg.type = Image.Type.Sliced; eimg.raycastTarget = false;
            var ert = eimg.rectTransform; ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one; ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
            return go;
        }

        private void Soft(Image img)
        {
            if (img == null) return;
            img.sprite = RoundedSprite();
            img.type = Image.Type.Sliced;
        }

        private Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int s = 32, r = 7;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(Mathf.Max(r - x, x - (s - 1 - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - y, y - (s - 1 - r)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            roundedSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return roundedSprite;
        }

        private static void ClearChildren(RectTransform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }
    }
}
