using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Core.Events;
using RPGArena.Characters;
using RPGArena.Combat.Commands;
using RPGArena.Combat.Events;

namespace RPGArena.Combat
{
    // The live, real-time battle driver for the playable game (the §4.6 FSM realised as a
    // coroutine). It reuses the M1 combat systems but, on a hero's turn, AWAITS player input
    // submitted by the HUD; the boss uses its Strategy AI brain. It raises the ScriptableObject
    // event channels the HUD/audio/juice subscribe to, and never reaches into presentation
    // directly. (The synchronous BattleManager remains for headless tests.)
    public class BattleController : MonoBehaviour
    {
        [Header("Content")]
        public BalanceConfig balance;
        public BossDefinition boss;                                   // fallback boss (direct-play)
        public List<BossDefinition> bossRoster = new();               // all bosses, picked by RunState
        public List<BoonDefinition> boonRoster = new();               // all boons, looked up by name
        public List<CharacterDefinition> roster = new();              // all selectable classes
        public List<string> defaultParty = new() { "Warrior", "Mage", "Thief" };

        [Header("Event channels (presentation subscribes)")]
        public VoidChannel onBattleStarted, onBattleWon, onBattleLost;
        public EntityChannel onTurnStarted, onTurnEnded, onEntityDied, onStaggerBroken;
        public DamageResultChannel onDamageDealt;
        public AbilityChannel onBossTelegraph;

        [Header("Pacing")]
        public float actionDelay = 0.55f;

        public BattleContext Context { get; private set; }
        public int RoundsTaken { get; private set; }                  // for the victory grade (§9.6)
        private List<Entity> currentOrder;          // this round's order; a bonus turn inserts mid-list
        private int orderIndex;                      // index of the actor currently taking their turn
        public IReadOnlyCollection<Entity> UpcomingOrder =>
            currentOrder != null && orderIndex < currentOrder.Count
                ? currentOrder.GetRange(orderIndex, currentOrder.Count - orderIndex)
                : System.Array.Empty<Entity>();      // next actors, for the HUD tracker
        public Entity ActiveHero { get; private set; }                // whose input we await (null otherwise)
        public bool AwaitingInput => ActiveHero != null && pendingAction == null;
        public BattleManager.Outcome Result { get; private set; } = BattleManager.Outcome.InProgress;

        private ActionRequest? pendingAction;
        private bool pendingReposition;          // HUD requested a row swap (front<->back) this turn

        // Set by the presentation layer (JuiceController) to the time the current strike's juice
        // finishes; the battle loop waits on it so the next turn never starts mid-animation.
        public static float PresentationBusyUntil;

        private void Start() => StartCoroutine(RunBattle());

        // The HUD calls this when the active hero chooses an ability + target.
        public void SubmitAction(Ability ability, Entity target)
        {
            if (ActiveHero == null || ability == null) return;
            // Re-validate cost/cooldown here too (not only in the HUD): an unaffordable or on-cooldown
            // submission must NOT silently consume the hero's whole turn — reject it, keep the menu open.
            if (!CanAfford(ActiveHero, ability)) return;
            pendingAction = new ActionRequest(ability, ActiveHero, ResolveTargets(ActiveHero, ability, target));
        }

        // Single source of truth for affordability (MP + cooldown), used by BOTH the controller (to
        // reject) and the HUD (to grey buttons out) so the two can never drift.
        public static bool CanAfford(Entity hero, Ability a)
            => hero != null && a != null && hero.currentMP >= a.mpCost && !hero.IsOnCooldown(a);

        // Convenience for a "pass/defend with no target" action.
        public void SubmitAction(Ability ability) => SubmitAction(ability, null);

        // The HUD calls this when the active hero chooses to reposition (swap front/back row).
        public void SubmitReposition() { if (ActiveHero != null) pendingReposition = true; }

        // The HUD's gold OVERDRIVE button: spend a FULL Valor meter on the party-wide damage surge.
        // A free activation — the hero still takes their action this turn (now surge-boosted), so you
        // "charge up, unleash, then dump a Shatter in the Break window" for the biggest reliable hit.
        public void SubmitOverdrive() { if (ActiveHero != null) Context?.charge?.SpendOverdrive(Context, ActiveHero); }

        private IEnumerator RunBattle()
        {
            PresentationBusyUntil = 0f;
            BuildContext();

            // Narrative intro (Ink): if a NarrativeRunner is present, wait for the player's
            // pre-fight choice, then let it alter the opening (§13.2). Found by interface so this
            // assembly never depends on the Narrative assembly.
            var intro = FindIntro();
            if (intro != null)
            {
                while (!intro.IsIntroDone) yield return null;
                if (intro.StartTelegraph && Context.boss != null)
                    Context.boss.aiCycleIndex = 2;          // open on Charging Breath (riskier, faster)
                if (intro.RevealWeak)
                {
                    Context.weaknessRevealed = true;        // HUD reveals weak/absorb elements now
                    // Derive the reveal from the ACTUAL boss profile (was hardcoded to the Dragon's
                    // ICE/FIRE, which would be wrong for every other boss in the gauntlet).
                    var prof = Context.boss != null ? Context.boss.elementProfile : null;
                    string weak = prof != null && prof.weakTo != null && prof.weakTo.Length > 0 ? string.Join("/", prof.weakTo) : "nothing obvious";
                    string absorb = prof != null && prof.absorbs != null && prof.absorbs.Length > 0 ? string.Join("/", prof.absorbs) : "nothing";
                    Context.Log($"You study {Context.boss.displayName}: weak to {weak}; absorbs {absorb}.");
                }
            }

            Context.Log($"=== {boss.bossName} appears! ===");
            onBattleStarted?.Raise();
            yield return Wait();

            for (int round = 1; round <= 60 && Result == BattleManager.Outcome.InProgress; round++)
            {
                RoundsTaken = round;
                var order = new List<Entity>(Context.turns.BuildRoundOrder(All(), Context.rng, balance.maxExtraTurnsPerEntityPerRound));
                currentOrder = order;   // exposed to the HUD's turn-order tracker
                for (orderIndex = 0; orderIndex < order.Count && Result == BattleManager.Outcome.InProgress; orderIndex++)
                {
                    var actor = order[orderIndex];
                    if (actor == null || !actor.IsAlive) continue;

                    int dot = actor.TickStartOfTurn();
                    if (dot > 0) Context.Log($"{actor.displayName} takes {dot} damage over time.");
                    onTurnStarted?.Raise(actor);

                    if (actor.CanAct)
                    {
                        ICommand cmd = null;
                        Ability used = null; Entity[] usedTargets = null;

                        if (actor.Brain != null)
                        {
                            // AI-controlled (the boss).
                            var opponents = actor.team == Team.Heroes ? new List<Entity> { Context.boss } : Context.heroes;
                            var ability = actor.Brain.DecideAction(Context, actor, opponents, out var tgt);
                            if (ability != null)
                            {
                                var req = new ActionRequest(ability, actor, ResolveTargets(actor, ability, tgt));
                                used = ability; usedTargets = req.targets; cmd = CommandFactory.Build(req);
                            }
                        }
                        else
                        {
                            // Player hero: open the action menu and wait for the HUD to submit an
                            // ability OR a reposition (swap row).
                            ActiveHero = actor;
                            pendingAction = null;
                            pendingReposition = false;
                            while (pendingAction == null && !pendingReposition) yield return null;
                            if (pendingReposition)
                            {
                                pendingReposition = false;
                                Reposition(actor);     // spends the turn; cmd stays null so no attack resolves
                                ActiveHero = null;
                            }
                            else
                            {
                                var req = pendingAction.Value;
                                used = req.ability; usedTargets = req.targets; cmd = CommandFactory.Build(req);
                                ActiveHero = null;
                                pendingAction = null;
                            }
                        }

                        if (cmd != null)
                        {
                            Context.lastActionResults.Clear();
                            Context.Log(cmd.DescribeForLog());
                            cmd.Resolve(Context);

                            // Non-damaging skills (buffs / heals / stances / defend) don't pass through
                            // OnDamageDealt, so play their cast animation + spawn their VFX here, so
                            // EVERY skill has presentation.
                            if (used != null && used.effectType != EffectType.Attack && used.effectType != EffectType.MultiHit && used.effectType != EffectType.BossMove)
                                PlayNonDamagingFx(actor, used, usedTargets);

                            // Action economy: each hero acts ONCE per round — no "+1 More" bonus turn
                            // (it made a 3-hero party take 4 actions every round). Exploiting a weakness
                            // or landing a combo now pays off through PARTY VALOR + the damage itself,
                            // not an extra turn, so the round stays a clean one-action-per-hero.

                            // Party Valor accrues from this action — coordination (weakness/combo/
                            // setup/buff) charges it hard, a plain spam-hit barely (null-safe).
                            if (actor.team == Team.Heroes)
                                ChargeSystem.AwardFor(used, Context.lastActionResults, Context);

                            yield return WaitForPresentation();
                        }
                    }
                    else
                    {
                        Context.Log($"{actor.displayName} is frozen/staggered — turn skipped.");
                        yield return Wait();
                    }

                    actor.TickEndOfTurn();
                    if (actor.team == Team.Heroes) Context.charge?.ConsumeHeroTurn(Context);   // count down an active Overdrive surge
                    if (actor.isBoss)   // Searing Fury escalates each boss turn; a Break vents it (StaggerSystem)
                    {
                        actor.rageStacks = Mathf.Min(actor.rageStacks + 1, balance.rageMaxStacks);
                        Context.Log($"    {actor.displayName}'s Searing Fury rises to {actor.rageStacks} (+{actor.rageStacks * balance.rageDamagePerStack * 100f:0}% damage — BREAK it to vent!)");
                    }
                    onTurnEnded?.Raise(actor);
                    CheckDeaths();
                    Context.boss?.CheckPhaseTransition(Context);
                    Result = Evaluate();
                }
            }

            bool won = Result == BattleManager.Outcome.Victory;
            if (won) { Context.Log($"=== VICTORY! {boss.bossName} is slain. ==="); onBattleWon?.Raise(); }
            else { Context.Log("=== DEFEAT. The party has fallen. ==="); onBattleLost?.Raise(); }

            // The RunFlow (presentation) owns the post-battle UX (boon select / run complete /
            // retry), driven by the OnBattleWon / OnBattleLost channels raised above.
        }

        // Find a narrative intro by interface (no compile-time dependency on the Narrative asm).
        private IBattleIntro FindIntro()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IBattleIntro bi) return bi;
            return null;
        }

        // --- setup --------------------------------------------------------------------
        private void BuildContext()
        {
            var run = GameBootstrap.Instance?.Run;

            // Boss: pick the run's current boss from the roster FIRST (so per-boss tuning like the
            // Break-window length is read from the right boss), else the fallback (direct-play).
            if (run != null && bossRoster != null && bossRoster.Count > 0)
            {
                var match = bossRoster.Find(b => b != null && b.name == run.CurrentBoss);
                if (match != null) boss = match;
            }

            Context = new BattleContext
            {
                balance = balance,
                rng = new System.Random(),
                echoToConsole = true,
                // The Break window lasts at least 2 of the boss's turns so a fast party still gets
                // a real burst round even if some heroes already acted before the Break landed (1.16).
                BossStaggeredTurns = Mathf.Max(1, boss.staggeredTurns),   // honor the boss asset's authored Break-window length
                damage = new DamagePipeline(balance, new System.Random()),
                stagger = new StaggerSystem(),
                turns = new TurnSystem(),
                charge = new ChargeSystem { max = balance.valorMax },   // party Valor / Overdrive (live battles only)
                onBattleStarted = onBattleStarted, onBattleWon = onBattleWon, onBattleLost = onBattleLost,
                onTurnStarted = onTurnStarted, onTurnEnded = onTurnEnded, onEntityDied = onEntityDied,
                onStaggerBroken = onStaggerBroken, onDamageDealt = onDamageDealt, onBossTelegraph = onBossTelegraph
            };

            // Party: from the run state's selection, else the default trio.
            var chosen = run != null && run.partyClassNames.Count > 0 ? run.partyClassNames : defaultParty;
            foreach (var className in chosen)
            {
                var def = roster.Find(c => c != null && c.className == className);
                if (def != null) Context.heroes.Add(BattleSpawner.SpawnHero(def, balance, null)); // null brain => player
            }

            // Roguelite boons: apply every boon collected this run to the whole party (Appendix E.2).
            if (run != null && boonRoster != null)
                foreach (var boonName in run.acquiredBoons)
                {
                    var bd = boonRoster.Find(b => b != null && b.name == boonName);
                    if (bd != null) foreach (var h in Context.heroes) BoonSystem.Apply(h, bd);
                }
            // Full restore between bosses (and top up after boon maxHP/MP increases).
            foreach (var h in Context.heroes) { h.currentHP = h.stats.maxHP; h.currentMP = h.stats.maxMP; }

            Context.boss = BattleSpawner.SpawnBoss(boss, balance);

            PlaceCombatants();
        }

        // Stage the combatants on the orthographic arena: heroes on the left facing the boss
        // on the right. Each gets a placeholder capsule "body" (swapped for real rigged models
        // when those are imported) so the fight is visible. Colours distinguish the classes.
        private static readonly Color[] HeroPalette =
        {
            new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.9f),
            new Color(0.6f, 0.35f, 0.8f), new Color(0.35f, 0.75f, 0.4f)
        };

        private void PlaceCombatants()
        {
            var bossPos = new Vector3(4.7f, 0f, 1.3f);   // boss anchor: a real gap to charge across, but inside the tighter 45° framing (x=6 hid it behind the skill panel)
            for (int i = 0; i < Context.heroes.Count; i++)
            {
                var h = Context.heroes[i];
                h.backRow = h.primaryStat != PrimaryStat.STR;                          // STR melee = front line; casters/ranged = back
                float rowZ = h.backRow ? 1.6f : -1.0f;                                 // formation: deeper front/back split so the rows read apart
                h.transform.position = new Vector3(-4.4f + i * 1.7f, 0f, rowZ);        // wider spacing so the heroes aren't shoulder-to-shoulder
                h.transform.rotation = Quaternion.Euler(0, 90, 0);
                Vector3 faceBoss = bossPos - h.transform.position; faceBoss.y = 0f;
                float scale = h.modelPrefab != null ? 1.2f : 1f;     // make the 3D heroes read larger
                AttachBody(h.gameObject, h.modelPrefab, h.stageSprite, HeroPalette[i % HeroPalette.Length], 1f, 1.9f, i, faceBoss, scale, 0f, 0.35f);
                var motion = h.gameObject.AddComponent<CombatantMotion>();    // lunge/recoil (+ procedural bob if no model)
                if (h.modelPrefab != null) motion.bobAmplitude = 0f;          // the Animator's Idle replaces the bob
            }
            if (Context.boss != null)
            {
                Context.boss.transform.position = bossPos;
                Context.boss.transform.rotation = Quaternion.Euler(0, -90, 0);
                Vector3 faceHeroes = (Context.heroes.Count > 0 ? Context.heroes[0].transform.position : Vector3.zero) - bossPos; faceHeroes.y = 0f;
                // The boss is BIG but still parses at a glance next to ~2.5u heroes: ~5u tall (2× hero)
                // reads as "huge monster" without swallowing the frame or clipping the camera. Mostly
                // face the heroes (low camera blend) so its attacks visibly aim at the party.
                AttachBody(Context.boss.gameObject, Context.boss.modelPrefab, Context.boss.stageSprite, new Color(0.5f, 0.12f, 0.12f), 2.0f, 3.0f, 0, faceHeroes, 1f, 5.0f, 0.18f);
                var bm = Context.boss.gameObject.AddComponent<CombatantMotion>();
                bm.lungeDistance = 0.8f;
                bm.bobAmplitude = Context.boss.modelPrefab != null ? 0f : 0.12f;   // a heavier-feeling 2D boss bobs
            }

            DressStage();
        }

        // Turns the bare stage into a place: a full-screen arena backdrop behind everyone and a
        // soft contact shadow under each combatant so they don't float (the 2.5D look, §10).
        private void DressStage()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Only fall back to the flat 2D sprite backdrop when there's no real 3D environment in
            // the scene. When an "ArenaEnvironment" (the Holotna 3D stage) is present, the skybox +
            // 3D meadow are the background, so the 2D quad is skipped (it would clash with the models).
            if (boss != null && boss.arenaBackdrop != null && GameObject.Find("Backdrop") == null
                && GameObject.Find("ArenaEnvironment") == null)
            {
                var bg = new GameObject("Backdrop");
                var sr = bg.AddComponent<SpriteRenderer>();
                sr.sprite = boss.arenaBackdrop;
                sr.color = new Color(0.7f, 0.7f, 0.75f);     // slightly dimmed so combatants pop
                sr.sortingOrder = -100;
                float viewH = 2f * cam.orthographicSize;
                float viewW = viewH * Mathf.Max(1.3f, cam.aspect);
                var size = boss.arenaBackdrop.bounds.size;
                float scale = Mathf.Max(viewW / size.x, viewH / size.y) * 1.08f;
                bg.transform.localScale = Vector3.one * scale;
                var cp = cam.transform.position;
                bg.transform.position = new Vector3(cp.x, cp.y, cp.z + 18f);
            }

            for (int i = 0; i < Context.heroes.Count; i++) AddShadow(Context.heroes[i].gameObject, 1.5f);
            if (Context.boss != null) AddShadow(Context.boss.gameObject, 2.8f);
        }

        // A camera-facing flattened dark blob at a combatant's feet — a cheap, readable contact shadow.
        private static void AddShadow(GameObject host, float width)
        {
            if (host.transform.Find("Shadow") != null) return;
            var go = new GameObject("Shadow");
            go.transform.SetParent(host.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShadowSprite();
            sr.color = new Color(0f, 0f, 0f, 0.55f);
            sr.sortingOrder = -1;
            // Lay the soft blob FLAT in the ground plane (XZ). The old code used a yaw-only
            // Inverse(host.rotation) which left the sprite quad STANDING VERTICAL — under the new
            // perspective camera that read as fighters floating with no contact patch. A world-space
            // Euler(90,0,0) lays it on the grass; the 3/4 camera naturally foreshortens it to an oval.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float sx = width / 0.64f;                       // shadow sprite is 64px @ 100ppu = 0.64u
            go.transform.localScale = new Vector3(sx, sx * 0.78f, 1f);   // near-round footprint, soft edge
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);     // a hair above the terrain (no z-fight)
        }

        // Procedural soft radial sprite (built once) used for the contact shadows.
        private static Sprite shadowSprite;
        private static Sprite ShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var c = new Vector2(s / 2f, s / 2f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s / 2f);
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    tex.SetPixel(x, y, new Color(0, 0, 0, a));
                }
            tex.Apply();
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return shadowSprite;
        }

        // Give a combatant a visible "body". If it has a stageSprite, billboard that full-body art
        // facing the camera (the 2.5D MapleStory look); otherwise fall back to a coloured capsule.
        // Cast animation + spell VFX for non-damaging skills (buffs / heals / stances / defend),
        // which don't flow through the damage event. Spawns the ability's VFX at the first target
        // (or the caster for self-buffs) and destroys it after a few seconds.
        private void PlayNonDamagingFx(Entity caster, Ability ability, Entity[] targets)
        {
            caster.GetComponentInChildren<RPGArena.Characters.AnimationDriver>()?.PlayCast();
            if (ability.vfxPrefab == null) return;
            // Buff/heal/aura prefabs are authored around the character's feet — ground them at EACH
            // recipient (a party-wide blessing should visibly bless the whole party, not just hero #1).
            bool any = false;
            if (targets != null)
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    any = true;
                    var fx = Instantiate(ability.vfxPrefab, t.transform.position + Vector3.up * 0.05f, Quaternion.identity);
                    Destroy(fx, 4f);
                }
            if (!any)
            {
                var fx = Instantiate(ability.vfxPrefab, caster.transform.position + Vector3.up * 0.05f, Quaternion.identity);
                Destroy(fx, 4f);
            }
        }

        private static void AttachBody(GameObject host, GameObject modelPrefab, Sprite sprite, Color color, float width, float height, int order, Vector3 faceDir, float modelScale = 1f, float targetModelHeight = 0f, float camBlend = 0.5f)
        {
            if (host.transform.Find("Body") != null) return;

            // Rigged 3D model (the real animated characters) — replaces the 2D billboard when present.
            if (modelPrefab != null)
            {
                var model = Instantiate(modelPrefab);
                model.name = "Body";
                model.transform.SetParent(host.transform, false);
                // 3/4 view: blend facing-the-foe with facing-the-camera so the model's FRONT shows
                // (not a dead-on profile). Yaw the model, not the camera, so the backdrop + shadow stay put.
                Vector3 faceFoe = faceDir; faceFoe.y = 0f;
                var cam = Camera.main;
                Vector3 faceCam = (cam != null ? cam.transform.position - host.transform.position : new Vector3(0, 0, -1)); faceCam.y = 0f;
                Vector3 look = faceFoe.sqrMagnitude > 0.0001f && faceCam.sqrMagnitude > 0.0001f
                    ? Vector3.Slerp(faceFoe.normalized, faceCam.normalized, camBlend)
                    : (faceFoe.sqrMagnitude > 0.0001f ? faceFoe.normalized : Vector3.forward);
                model.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                if (modelScale > 0f && !Mathf.Approximately(modelScale, 1f)) model.transform.localScale *= modelScale;
                // Auto-scale to a target world height (used to make the boss dragon big and looming
                // regardless of the source model's native size). Measured from the model's renderers.
                if (targetModelHeight > 0f)
                {
                    var rends = model.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        if (b.size.y > 0.001f) model.transform.localScale *= targetModelHeight / b.size.y;
                    }
                }
                model.transform.localPosition = Vector3.zero;   // prefab pivot is already at the feet
                // Safety net for imported clips with big vertical hip root-motion (Generic rigs sink
                // into the floor when applyRootMotion is off) — clamps the hips to ~bind height.
                model.AddComponent<Characters.HipHeightLock>();
                return;
            }

            if (sprite != null)
            {
                var bb = new GameObject("Body");
                bb.transform.SetParent(host.transform, false);
                var sr = bb.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
                // Face the camera (cancel the host's facing rotation) and scale to the target height.
                bb.transform.localRotation = Quaternion.Inverse(host.transform.rotation);
                float spriteH = sprite.bounds.size.y;
                float s = spriteH > 0f ? height / spriteH : 1f;
                bb.transform.localScale = Vector3.one * s;
                bb.transform.localPosition = new Vector3(0, height * 0.5f, 0);   // feet on the ground
                return;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            var col = body.GetComponent<Collider>(); if (col) Destroy(col);
            body.transform.SetParent(host.transform, false);
            body.transform.localScale = new Vector3(width, height * 0.5f, width);
            body.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // --- helpers ------------------------------------------------------------------
        private Entity[] ResolveTargets(Entity actor, Ability ability, Entity single)
        {
            var opponents = actor.team == Team.Heroes ? new List<Entity> { Context.boss } : Context.heroes;
            switch (ability.targetRule)
            {
                case TargetRule.AllEnemies: return TargetingSystem.AllAlive(opponents).ToArray();
                case TargetRule.AllAllies:
                    var allies = actor.team == Team.Heroes ? Context.heroes : new List<Entity> { Context.boss };
                    return TargetingSystem.AllAlive(allies).ToArray();
                case TargetRule.Self: return new[] { actor };
                case TargetRule.SingleAlly: return new[] { single != null ? single : actor };
                default:
                    var t = single != null ? single : TargetingSystem.FirstAlive(opponents);
                    return t != null ? new[] { t } : new Entity[0];
            }
        }

        // Swap a hero between the front and back row mid-battle (the positioning move). Slides the
        // model to the new row and re-bases its procedural motion so it holds the new spot.
        private void Reposition(Entity hero)
        {
            if (hero == null) return;
            hero.backRow = !hero.backRow;
            var p = hero.transform.position;
            var np = new Vector3(p.x, 0f, hero.backRow ? 1.6f : -1.0f);
            hero.transform.position = np;
            hero.GetComponent<CombatantMotion>()?.MoveBase(np);
            Context.Log($"{hero.displayName} repositions to the {(hero.backRow ? "back" : "front")} row.");
        }

        private List<Entity> All()
        {
            var all = new List<Entity>(Context.heroes);
            if (Context.boss != null) all.Add(Context.boss);
            return all;
        }

        private readonly HashSet<Entity> announced = new();
        private void CheckDeaths()
        {
            foreach (var e in All())
                if (!e.IsAlive && announced.Add(e)) { Context.Log($"{e.displayName} has fallen."); onEntityDied?.Raise(e); }
        }

        private BattleManager.Outcome Evaluate()
        {
            if (Context.BossDead) return BattleManager.Outcome.Victory;
            if (Context.AllHeroesDead) return BattleManager.Outcome.Defeat;
            return BattleManager.Outcome.InProgress;
        }

        private WaitForSeconds Wait() => new WaitForSeconds(actionDelay);

        // Pace an action by the base beat AND the presentation layer's juice, so the strike's
        // approach -> impact -> recovery finishes before the next turn. Capped so a stuck flag
        // (e.g. a missing JuiceController) can never hang the fight.
        private IEnumerator WaitForPresentation()
        {
            yield return Wait();
            float guard = Time.time + 2.5f;
            while (Time.time < PresentationBusyUntil && Time.time < guard) yield return null;
            yield return new WaitForSeconds(0.12f);
        }
    }
}
