using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Status;
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
        private Text bossName, bossHpText, log, telegraph, bossWeakness, bossStaggerText;
        private Image bossHpFill, bossStaggerFill;
        private GameObject telegraphPanel;
        private Sprite roundedSprite;
        private bool weaknessSeen;          // a weakness hit has landed (or the player studied)
        private readonly List<Text> partyTexts = new();
        private readonly List<Image> partyHpFill = new();
        private readonly List<Image> partyMpFill = new();
        private readonly List<Text> partyHpText = new();
        private readonly List<Text> partyMpText = new();
        private RectTransform actionPanel;
        private struct LogEntry { public string text; public Color color; }
        private readonly List<LogEntry> logEntries = new();
        private RectTransform bossStatusRow;
        private readonly List<RectTransform> heroStatusRows = new();
        private readonly string[] statusSigs = new string[8];   // 0 = boss, 1.. = heroes
        private RectTransform turnOrderRow;
        private string turnOrderSig = "";
        // Animated "chip-away" bars: each coloured fill has a pale ghost behind it that trails on a
        // hit (the visible gap is the damage chunk), and both ease toward a target instead of snapping.
        private readonly Dictionary<Image, Image> ghostOf = new();
        private readonly Dictionary<Image, float> targetFill = new();

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
            // Win/lose end-screens are owned by RunFlow (boon select / run complete / retry), so
            // the HUD no longer shows its own result panel.
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
            AnimateBars();

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
            if (!r.hit) AddLog($"{who}  miss", new Color(0.60f, 0.60f, 0.64f));
            else if (r.absorbed) AddLog($"{who}  ABSORBED {r.amount} — healed", new Color(0.55f, 0.80f, 1f));
            else if (r.isHeal) AddLog($"{who}  +{r.amount} HP", new Color(0.45f, 0.85f, 0.42f));
            else
            {
                string suffix = (r.reaction == ElementReaction.Weak ? "  WEAK" : "") + (r.crit ? "  CRIT" : "");
                Color c = r.crit ? new Color(1f, 0.82f, 0.25f)                       // gold = crit
                        : r.reaction == ElementReaction.Weak ? new Color(0.40f, 0.85f, 1f)  // cyan = weakness
                        : new Color(0.84f, 0.86f, 0.90f);                            // muted = plain hit
                AddLog($"{who}  -{r.amount}{suffix}", c);
            }
            if (r.reaction == ElementReaction.Weak) weaknessSeen = true;   // reveal it in the HUD
        }

        private void OnBreak(Entity boss) => AddLog($"BREAK!  {boss.displayName} staggered", new Color(1f, 0.68f, 0.18f));
        private void OnDied(Entity e) => AddLog($"{e.displayName} has fallen", new Color(1f, 0.40f, 0.38f));

        private void OnTelegraph(Ability a)
        {
            if (telegraph == null) return;
            telegraph.text = $"⚠  DRAGON IS CHARGING: {a.displayName.ToUpper()}  —  BREAK IT OR DEFEND  ⚠";
            telegraph.gameObject.SetActive(true);
            if (telegraphPanel != null) telegraphPanel.SetActive(true);
            // Visibility is now STATE-driven (see RefreshBars): the banner stays up across the
            // multiple hero turns until the Dragon unleashes the move or it is Broken — not a timer.
        }
        private void HideTelegraphNow()
        {
            if (telegraph != null) telegraph.gameObject.SetActive(false);
            if (telegraphPanel != null) telegraphPanel.SetActive(false);
        }

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
                // Stagger meter: labelled, and it visibly changes state (pulses white) while Broken so
                // the headline Break window is legible, not a decorative underline.
                if (bossStaggerText != null)
                    bossStaggerText.text = ctx.boss.isStaggered
                        ? "★  BROKEN  ★"
                        : $"BREAK  {Mathf.RoundToInt(ctx.boss.staggerMeter)} / {Mathf.RoundToInt(ctx.boss.staggerThreshold)}";
                if (bossStaggerFill != null)
                    bossStaggerFill.color = ctx.boss.isStaggered
                        ? Color.Lerp(new Color(1f, 0.96f, 0.6f), Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f))
                        : new Color(0.95f, 0.8f, 0.2f);
                if (bossWeakness != null)
                    bossWeakness.text = (weaknessSeen || ctx.weaknessRevealed) ? FormatWeakness(ctx.boss) : "";
                RefreshStatusRow(bossStatusRow, ctx.boss, 0, true);

                // Telegraph banner stays up until the charged move FIRES or is BROKEN (state-driven,
                // not a 3.5s timer that vanished before the player could react across slow boss turns).
                bool charging = ctx.boss.telegraphedAbility != null;
                if (!charging && telegraph != null && telegraph.gameObject.activeSelf) HideTelegraphNow();
            }
            RefreshTurnOrder();
            for (int i = 0; i < partyTexts.Count; i++)
            {
                if (i >= ctx.heroes.Count) { partyTexts[i].transform.parent.gameObject.SetActive(false); continue; }
                var h = ctx.heroes[i];
                bool active = controller.ActiveHero == h;
                partyTexts[i].text = $"{(active ? "> " : "")}{h.displayName}   [{(h.backRow ? "BACK" : "FRONT")}]";
                partyTexts[i].color = h.IsAlive ? (active ? Color.yellow : Color.white) : new Color(0.5f, 0.5f, 0.5f);
                SetFill(partyHpFill[i], h.currentHP, h.stats.maxHP);
                SetFill(partyMpFill[i], h.currentMP, h.stats.maxMP);
                if (i < partyHpText.Count) partyHpText[i].text = h.IsAlive ? $"{h.currentHP}/{h.stats.maxHP}" : "— KO —";
                if (i < partyMpText.Count) partyMpText[i].text = $"MP {h.currentMP}/{h.stats.maxMP}";
                if (i < heroStatusRows.Count) RefreshStatusRow(heroStatusRows[i], h, i + 1, false);
            }
        }

        // --- turn-order tracker -------------------------------------------------------
        private void RefreshTurnOrder()
        {
            if (turnOrderRow == null || controller.UpcomingOrder == null) return;
            // Rebuild only when the upcoming line-up changes (cheap signature).
            var sb = new System.Text.StringBuilder();
            int seen = 0;
            foreach (var e in controller.UpcomingOrder) { if (e != null && e.IsAlive) { sb.Append(e.displayName).Append('|'); if (++seen >= 6) break; } }
            string sig = sb.ToString();
            if (sig == turnOrderSig) return;
            turnOrderSig = sig;

            ClearChildren(turnOrderRow);
            int i = 0;
            foreach (var e in controller.UpcomingOrder)
            {
                if (e == null || !e.IsAlive) continue;
                var chip = new GameObject("Chip"); chip.transform.SetParent(turnOrderRow, false);
                var img = chip.AddComponent<Image>();
                img.color = e.isBoss ? new Color(0.5f, 0.16f, 0.16f, 0.88f) : new Color(0.15f, 0.2f, 0.32f, 0.82f);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -i * 30); rt.sizeDelta = new Vector2(180, 27);
                var t = MakeText(rt, (i == 0 ? "▶ " : $"{i + 1}. ") + e.displayName, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(172, 24), 14, TextAnchor.MiddleLeft);
                t.color = i == 0 ? Color.yellow : Color.white;
                if (++i >= 6) break;
            }
        }

        // --- status-effect badges -----------------------------------------------------
        // Rebuild a combatant's status badge row only when its set of statuses changes (cheap sig).
        private void RefreshStatusRow(RectTransform row, Entity e, int sigKey, bool big)
        {
            if (row == null || e == null) return;
            string sig = StatusSig(e);
            if (sig == statusSigs[sigKey]) return;
            statusSigs[sigKey] = sig;
            ClearChildren(row);
            var fx = e.Status.Effects;
            float step = big ? 62f : 46f;
            float startX = big ? Mathf.Max(0f, (row.sizeDelta.x - fx.Count * step) / 2f) : 0f;
            for (int i = 0; i < fx.Count; i++) MakeBadge(row, fx[i], startX + i * step, big);
        }

        private void MakeBadge(RectTransform parent, StatusEffectContainer.Active a, float x, bool big)
        {
            float w = big ? 58f : 42f, h = big ? 22f : 15f;
            var go = new GameObject("Badge"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = KindColor(a.def.kind);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, h);
            string lbl = Abbrev(a.def.displayName) + (a.stacks > 1 ? a.stacks.ToString() : "") + (a.remaining < 90 ? " " + a.remaining : "");
            var t = MakeText(rt, lbl, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), big ? 12 : 10, TextAnchor.MiddleCenter);
            t.color = Color.white;
        }

        private static string Abbrev(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, Mathf.Min(3, name.Length)).ToUpper();

        // Colour-codes the badge by kind (paired with the abbreviation for colourblind safety, §9.8).
        private static Color KindColor(StatusKind k) =>
            k == StatusKind.Buff ? new Color(0.24f, 0.66f, 0.34f, 0.92f) :
            k == StatusKind.Debuff ? new Color(0.8f, 0.3f, 0.3f, 0.92f) :
            k == StatusKind.DoT ? new Color(0.9f, 0.55f, 0.2f, 0.92f) :
            k == StatusKind.Control ? new Color(0.55f, 0.35f, 0.85f, 0.92f) :
            new Color(0.28f, 0.6f, 0.85f, 0.92f);   // Flag

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
            var go = new GameObject("StatusRow"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
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
                if (IsDamaging(ab)) label += BuildPreview(hero, ab, ctx);

                var btn = MakeButton(actionPanel, label, new Vector2(0, -y), affordable, ab.icon);
                if (affordable)
                    btn.onClick.AddListener(() => controller.SubmitAction(ab, PickTarget(hero, ab)));
                y += 30f;
            }
            // Positioning: swap the active hero's row (front <-> back). Spends the turn.
            var moveBtn = MakeButton(actionPanel, $"↕ Move to {(hero.backRow ? "FRONT" : "BACK")} row", new Vector2(0, -y), true, null);
            moveBtn.onClick.AddListener(() => controller.SubmitReposition());
        }

        // --- helpers ------------------------------------------------------------------
        private static bool IsDamaging(Ability a) => a.effectType == EffectType.Attack || a.effectType == EffectType.MultiHit;

        // The action-menu preview: hit% + the damage band + reaction (§E.1 informed gamble). ABSORB
        // is ALWAYS shown (never let the player heal the boss blind); WEAK/resist follow the
        // progressive weakness reveal (§4.11). The band naturally hints the rest.
        private string BuildPreview(Entity hero, Ability ab, BattleContext ctx)
        {
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
            string tag = pv.absorb ? "  ABSORB!"
                       : revealed && pv.reaction == ElementReaction.Weak ? "  WEAK"
                       : revealed && pv.reaction == ElementReaction.Resist ? "  resist"
                       : revealed && pv.reaction == ElementReaction.Immune ? "  immune" : "";
            string dmg = pv.absorb ? "heals!" : ab.hits > 1 ? $"{pv.min}-{pv.max}x{ab.hits}" : $"{pv.min}-{pv.max}";
            return $"   {hitPct}%  {dmg}{tag}";
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
                default: return ctx.boss;   // SingleEnemy / AoE resolved by the controller
            }
        }

        private void AddLog(string line) => AddLog(line, new Color(0.82f, 0.84f, 0.88f));

        // A short, color-coded event ticker (not a debug console): newest line at the bottom and
        // fully bright, older lines fade out — so the eye lands on what just happened.
        private void AddLog(string line, Color color)
        {
            logEntries.Add(new LogEntry { text = line, color = color });
            while (logEntries.Count > 5) logEntries.RemoveAt(0);
            if (log) log.text = ComposeLog();
        }

        private string ComposeLog()
        {
            var sb = new System.Text.StringBuilder();
            int n = logEntries.Count;
            for (int i = 0; i < n; i++)
            {
                float a = n <= 1 ? 1f : Mathf.Lerp(0.35f, 1f, (float)i / (n - 1));   // oldest dim -> newest bright
                var c = logEntries[i].color; c.a = a;
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(c)).Append('>')
                  .Append(logEntries[i].text).Append("</color>");
                if (i < n - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        // Record the bar's target fill; the coloured fill + pale ghost ease toward it in AnimateBars().
        private void SetFill(Image img, float cur, float max)
        {
            if (img) targetFill[img] = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
        }

        // Drive every bar toward its target (responsive, not the old per-frame snap): the coloured
        // fill drops quickly on a hit, the pale ghost trails behind to show the lost-HP chunk drain.
        // Uses unscaled time so the bars keep animating during the hit-stop freeze-frame.
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
                    else ghost.fillAmount = fill.fillAmount;   // healing / refill: ghost rises with the fill
                }
            }
        }

        // Build the "Weak: Ice • Absorbs: Fire" hint from the boss's element profile.
        private static string FormatWeakness(Entity boss)
        {
            if (boss == null || boss.elementProfile == null) return "";
            string weak = Join(boss.elementProfile.weakTo);
            string absorb = Join(boss.elementProfile.absorbs);
            string s = "";
            if (weak.Length > 0) s += $"Weak: {weak}";
            if (absorb.Length > 0) s += (s.Length > 0 ? "     " : "") + $"Absorbs: {absorb}";
            return s;
        }

        private static string Join(ElementType[] arr) =>
            arr == null || arr.Length == 0 ? "" : string.Join("/", arr);

        // ============================ UI construction ================================
        private void BuildUI()
        {
            // Ensure an EventSystem exists so the buttons are clickable (new Input System).
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
            // Lower reference resolution => the whole HUD renders ~1.5x larger (was rendering tiny).
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Boss panel (top) — dark backing so it reads over the arena backdrop.
            MakePanel(root, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(840, 148), new Color(0.04f, 0.04f, 0.07f, 0.55f));
            bossName = MakeText(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(780, 44), 34, TextAnchor.MiddleCenter);
            bossName.color = new Color(1f, 0.58f, 0.48f); bossName.fontStyle = FontStyle.Bold;
            bossHpFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(780, 28), new Color(0.85f, 0.18f, 0.18f));
            bossHpText = MakeText(root, "HP", new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(780, 28), 16, TextAnchor.MiddleCenter);
            bossStaggerFill = MakeBar(root, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(780, 14), new Color(0.95f, 0.8f, 0.2f));
            bossStaggerText = MakeText(root, "BREAK", new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(780, 14), 11, TextAnchor.MiddleCenter);
            bossStaggerText.color = Color.white; bossStaggerText.fontStyle = FontStyle.Bold;
            bossWeakness = MakeText(root, "", new Vector2(0.5f, 1f), new Vector2(0, -126), new Vector2(700, 22), 16, TextAnchor.MiddleCenter);
            bossWeakness.color = new Color(0.4f, 0.9f, 1f);
            // Telegraph WARNING banner: a saturated red bar behind bold amber text. High contrast and
            // state-driven so the "break it / defend it" decision is loud and persistent.
            telegraphPanel = MakePanel(root, new Vector2(0.5f, 1f), new Vector2(0, -152), new Vector2(760, 40), new Color(0.55f, 0.06f, 0.06f, 0.93f));
            telegraph = MakeText(root, "", new Vector2(0.5f, 1f), new Vector2(0, -152), new Vector2(900, 38), 22, TextAnchor.MiddleCenter);
            telegraph.color = new Color(1f, 0.93f, 0.4f); telegraph.fontStyle = FontStyle.Bold;
            telegraphPanel.SetActive(false);
            telegraph.gameObject.SetActive(false);
            // Boss status-effect badges (so Oiled/Wet/Marked/Frozen are visible for combos, §9.4).
            bossStatusRow = MakeRow(root, new Vector2(0.5f, 1f), new Vector2(0, -176), new Vector2(820, 26));

            // Turn-order tracker (top-right): plan around the boss's next turn / a Break (§9.4).
            MakePanel(root, new Vector2(1f, 1f), new Vector2(-14, -14), new Vector2(196, 232), new Color(0.04f, 0.04f, 0.07f, 0.55f));
            MakeText(root, "TURN ORDER", new Vector2(1f, 1f), new Vector2(-112, -28), new Vector2(180, 22), 16, TextAnchor.MiddleCenter).color = new Color(0.8f, 0.85f, 1f);
            turnOrderRow = MakeRow(root, new Vector2(1f, 1f), new Vector2(-112, -48), new Vector2(184, 184));

            // Party panel (bottom-left): up to 3 hero strips.
            for (int i = 0; i < 3; i++)
            {
                var strip = MakePanel(root, new Vector2(0f, 0f), new Vector2(20, 36 + i * 92), new Vector2(360, 86), new Color(0.05f, 0.06f, 0.1f, 0.62f));
                var st = strip.GetComponent<RectTransform>();
                partyTexts.Add(MakeText(st, "Hero", new Vector2(0f, 1f), new Vector2(110, -4), new Vector2(240, 22), 18, TextAnchor.MiddleLeft));
                partyHpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(118, -30), new Vector2(230, 16), new Color(0.3f, 0.8f, 0.3f)));
                partyMpFill.Add(MakeBar(st, new Vector2(0f, 1f), new Vector2(118, -52), new Vector2(230, 12), new Color(0.3f, 0.5f, 0.9f)));
                partyHpText.Add(MakeText(st, "", new Vector2(0f, 1f), new Vector2(118, -30), new Vector2(230, 16), 12, TextAnchor.MiddleCenter));
                partyMpText.Add(MakeText(st, "", new Vector2(0f, 1f), new Vector2(118, -52), new Vector2(230, 12), 10, TextAnchor.MiddleCenter));
                heroStatusRows.Add(MakeRow(st, new Vector2(0f, 0f), new Vector2(116, 6), new Vector2(240, 16)));
            }

            // Action menu (bottom-right).
            var menu = MakePanel(root, new Vector2(1f, 0f), new Vector2(-20, 30), new Vector2(390, 330), new Color(0.05f, 0.06f, 0.1f, 0.62f));
            actionPanel = menu.GetComponent<RectTransform>();

            // Combat log (left-middle): a short color-coded ticker on a solid-reading backing.
            MakePanel(root, new Vector2(0f, 0.5f), new Vector2(16, -64), new Vector2(420, 150), new Color(0.04f, 0.04f, 0.07f, 0.62f));
            log = MakeText(root, "", new Vector2(0f, 0.5f), new Vector2(28, 18), new Vector2(396, 132), 16, TextAnchor.LowerLeft);
            log.supportRichText = true; log.lineSpacing = 1.15f;
            // (End-of-battle screens are owned by RunFlow, driven by OnBattleWon/OnBattleLost.)
        }

        private Text MakeText(RectTransform parent, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = content; t.fontSize = fontSize; t.alignment = align; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            // A dark outline keeps text legible over the busy arena backdrop.
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.85f); o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private Image MakeBar(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var bg = new GameObject("BarBG"); bg.transform.SetParent(parent, false);
            var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(0, 0, 0, 0.6f); Style(bgImg);
            var brt = bgImg.rectTransform; brt.anchorMin = brt.anchorMax = anchor; brt.pivot = anchor; brt.anchoredPosition = pos; brt.sizeDelta = size;
            // Ghost ("chip") layer behind the real fill: a pale bar that trails to reveal lost HP.
            var gh = new GameObject("BarGhost"); gh.transform.SetParent(bg.transform, false);
            var ghImg = gh.AddComponent<Image>(); ghImg.color = new Color(1f, 1f, 1f, 0.55f); ghImg.type = Image.Type.Filled; ghImg.fillMethod = Image.FillMethod.Horizontal; ghImg.fillOrigin = 0; ghImg.fillAmount = 1f;
            var grt = ghImg.rectTransform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = new Vector2(3, 3); grt.offsetMax = new Vector2(-3, -3);
            var fg = new GameObject("BarFill"); fg.transform.SetParent(bg.transform, false);
            var img = fg.AddComponent<Image>(); img.color = color; img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f;
            var frt = img.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            ghostOf[img] = ghImg; targetFill[img] = 1f;
            return img;
        }

        private GameObject MakePanel(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color; Style(img);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        private Button MakeButton(RectTransform parent, string label, Vector2 pos, bool enabled, Sprite icon = null)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = enabled ? new Color(0.16f, 0.3f, 0.5f, 0.9f) : new Color(0.25f, 0.25f, 0.25f, 0.7f); Style(img);
            var rt = img.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(0, 28); rt.offsetMin = new Vector2(6, rt.offsetMin.y); rt.offsetMax = new Vector2(-6, rt.offsetMax.y);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.interactable = enabled;
            // Tint on hover/press so the menu feels responsive (normal/disabled keep the base color).
            var cb = btn.colors; cb.normalColor = Color.white; cb.disabledColor = Color.white;
            cb.highlightedColor = new Color(0.72f, 0.86f, 1f); cb.pressedColor = new Color(0.55f, 0.68f, 0.85f);
            cb.fadeDuration = 0.08f; btn.colors = cb;

            float textLeft = 10f;
            if (icon != null)
            {
                var ig = new GameObject("Icon"); ig.transform.SetParent(rt, false);
                var iimg = ig.AddComponent<Image>(); iimg.sprite = icon; iimg.preserveAspect = true;
                var irt = iimg.rectTransform; irt.anchorMin = new Vector2(0, 0.5f); irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(4, 0); irt.sizeDelta = new Vector2(24, 24);
                textLeft = 32f;
            }

            var t = MakeText(rt, label, new Vector2(0, 0.5f), new Vector2(textLeft, 0), new Vector2(340, 26), 15, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(textLeft, 0); t.rectTransform.offsetMax = new Vector2(-6, 0);
            return btn;
        }

        // Apply a soft rounded-rectangle frame to any HUD Image (9-sliced so corners never stretch).
        // One shared sprite replaces the hard 90-degree opaque rectangles that read as programmer-art.
        private void Style(Image img)
        {
            if (img == null) return;
            img.sprite = RoundedSprite();
            img.type = Image.Type.Sliced;
        }

        private Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int s = 32, r = 6;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // distance into the nearest rounded corner (0 along the straight edges)
                    float dx = Mathf.Max(Mathf.Max(r - x, x - (s - 1 - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - y, y - (s - 1 - r)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);   // solid inside, 1px anti-aliased corner falloff
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
