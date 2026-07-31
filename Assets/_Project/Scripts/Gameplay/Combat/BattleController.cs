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
        public List<ItemDefinition> itemCatalog = new();              // all items, resolved by asset name (RunState stores names)

        [Header("Event channels (presentation subscribes)")]
        public VoidChannel onBattleStarted, onBattleWon, onBattleLost;
        public EntityChannel onTurnStarted, onTurnEnded, onEntityDied, onStaggerBroken;
        public DamageResultChannel onDamageDealt;
        public AbilityChannel onBossTelegraph;
        // Plain-English beats for the centre-screen announcer: "Warrior used Power Strike on The
        // Dragon", "ENEMY PHASE", "The Dragon is FROZEN SOLID". Null-safe like every channel.
        public StringChannel onAnnouncement;

        [Header("Pacing")]
        public float actionDelay = 0.55f;

        public BattleContext Context { get; private set; }
        public int RoundsTaken { get; private set; }                  // for the victory grade (§9.6)

        // THE ROUND IS TWO PHASES, not one initiative queue.
        //
        // PLAYER PHASE: the player picks WHICH hero acts next — three turns, any order, each hero
        // once. The old speed-roll queue meant the round where your Warrior came up before the
        // Mage's freeze simply could not SHATTER, and the HOLD patch that fixed it was a workaround
        // for an ordering the player never wanted. Free ordering subsumes HOLD entirely.
        // ENEMY PHASE: minions strike first, the boss lands the climax blow, every enemy acts
        // VISIBLY every round — no silent skips, ever. A boss that cannot act (Frozen / Broken)
        // says so on screen instead of vanishing from the queue.
        //
        // The headless BattleManager keeps the speed-based order: the balance gate measures the
        // fight without the player's ordering advantage, which Hard difficulty then prices in.
        public bool AwaitingHeroPick { get; private set; }            // the HUD shows "choose a hero"
        private readonly List<Entity> heroesToAct = new();            // living heroes yet to act this round
        public IReadOnlyList<Entity> HeroesYetToAct => heroesToAct;
        private Entity pickedHero;
        private readonly List<Entity> enemyPhaseOrder = new();        // minions first, boss last
        public IReadOnlyList<Entity> EnemyPhaseOrder => enemyPhaseOrder;   // the HUD's intent tracker

        public Entity ActiveHero { get; private set; }                // whose input we await (null otherwise)
        public bool AwaitingInput => ActiveHero != null && pendingAction == null;
        public BattleManager.Outcome Result { get; private set; } = BattleManager.Outcome.InProgress;

        private ActionRequest? pendingAction;
        private bool pendingReposition;          // HUD requested a row swap (front<->back) this turn
        private bool cancelPick;                 // HUD backed out of the action menu to re-pick a hero

        // Set by the presentation layer (JuiceController) to the time the current strike's juice
        // finishes; the battle loop waits on it so the next turn never starts mid-animation.
        public static float PresentationBusyUntil;

        // Play-testing this scene directly is the normal workflow here, and it skips Boot entirely.
        // Bringing the services up in Awake — before any Start reads them — is what gives a direct
        // fight its music, its gold and its item button instead of a silent, empty-satchel battle.
        private void Awake() => GameBootstrap.EnsureRuntime();

        private void Start() => StartCoroutine(RunBattle());

        // The HUD calls this when the active hero chooses an ability + target.
        public void SubmitAction(Ability ability, Entity target) => SubmitAction(ability, target, 1f);

        // Timed-strike variant: the HUD's action-command bar passes the multiplier it earned
        // (PERFECT ×1.18 … sloppy ×0.9). Logic stays deterministic — the mult rides the request.
        public void SubmitAction(Ability ability, Entity target, float timingMult)
            => SubmitAction(ability, target, timingMult, RiskStake.Press);

        // Stake variant: the HUD offers STEADY / PRESS / ALL IN before a risk-die SPECIAL resolves.
        public void SubmitAction(Ability ability, Entity target, float timingMult, RiskStake stake)
        {
            if (ActiveHero == null || ability == null) return;
            // Re-validate cost/cooldown here too (not only in the HUD): an unaffordable or on-cooldown
            // submission must NOT silently consume the hero's whole turn — reject it, keep the menu open.
            if (!CanAfford(ActiveHero, ability)) return;
            pendingAction = new ActionRequest(ability, ActiveHero, timingMult, stake, ResolveTargets(ActiveHero, ability, target));
        }

        // --- BRACE (the defensive action command) --------------------------------------
        // When an enemy commits to a damaging blow, a short reaction beat opens and the HUD runs a
        // timed-block needle inside it. Blocking is EARNED, not granted: the HUD converts press
        // timing into a multiplier (perfect block ×0.55, block ×0.78, bad/no press = full damage)
        // and submits it here. Live battles only — headless tests never run this coroutine.
        [Header("Action commands")]
        public float braceWindow = 1.0f;
        private float braceOpenUntil;
        private bool braceLanded;
        private float braceMult = 1f;
        public bool BraceWindowOpen => Time.time < braceOpenUntil && !braceLanded;
        public bool BraceLanded => braceLanded && braceMult < 1f && Time.time < braceOpenUntil + 0.6f;
        public float BraceTimeLeft => Mathf.Max(0f, braceOpenUntil - Time.time);
        // Pause freezes Time.time, so the window would otherwise stay "open" forever behind the
        // pause menu — reject braces submitted while the game is paused (no free reads).
        public void SubmitBrace(float mult)
        {
            if (Time.time >= braceOpenUntil || GamePause.IsPaused || braceLanded) return;
            braceLanded = true;
            braceMult = Mathf.Clamp(mult, 0.4f, 1f);
        }

        // --- FOLLOW-UP STRIKE (the third action command) ---------------------------------
        // Sometimes a landed blow leaves an opening: after a random beat, a short PRESS! prompt
        // flashes, and hitting it lands a bonus echo strike at a fraction of the skill's power.
        // Randomness (does the opening appear, and WHEN) plus reaction skill (the window is short) —
        // the same recipe as the timing bar, on the other side of the impact.
        [Header("Follow-up strike")]
        [Range(0f, 1f)] public float followUpChance = 0.45f;
        public float followUpWindow = 0.30f;
        [Range(0f, 1f)] public float followUpPowerFraction = 0.35f;
        private bool followUpOpen;
        private float followUpCloseAt;
        private bool followUpPressed;
        public bool FollowUpPromptOpen => followUpOpen && Time.time < followUpCloseAt;
        public void SubmitFollowUp() { if (FollowUpPromptOpen && !GamePause.IsPaused) followUpPressed = true; }

        private IEnumerator FollowUpOpportunity(Entity actor, Ability used, Entity target)
        {
            if (actor == null || used == null || target == null || !target.IsAlive || target.team == actor.team) yield break;
            bool anyHit = false;
            foreach (var r in Context.lastActionResults) if (r.hit && !r.isHeal) { anyHit = true; break; }
            if (!anyHit) yield break;
            // Presentation-layer rng on purpose: the logic stream (ctx.rng) stays untouched, so AI
            // decision parity with the headless loop is unaffected.
            if (Random.value > followUpChance) yield break;

            // A random tell delay — the prompt cannot be pressed on rhythm, only on reaction.
            yield return new WaitForSeconds(Random.Range(0.25f, 0.6f));
            // The ACTOR must still be standing too: a self-recoil backfire can kill the striker on
            // their own action, and a corpse does not get an encore.
            if (!actor.IsAlive || !target.IsAlive || Result != BattleManager.Outcome.InProgress) yield break;

            followUpPressed = false;
            followUpOpen = true;
            followUpCloseAt = Time.time + followUpWindow;
            while (Time.time < followUpCloseAt && !followUpPressed) yield return null;
            followUpOpen = false;
            if (!followUpPressed || !target.IsAlive) yield break;

            // The echo strike: a real hit through the real pipeline — no statuses, no risk die,
            // guaranteed to land (it is a reward, not a second gamble).
            Announce($"{actor.displayName} — FOLLOW-UP STRIKE!");
            actor.GetComponentInChildren<Characters.AnimationDriver>()?.PlayAttack();
            var element = used.followsAttunement ? actor.currentAttunement : used.element;
            Context.lastActionResults.Clear();
            var info = new DamageInfo
            {
                source = actor, target = target, ability = used, element = element,
                basePower = used.power * followUpPowerFraction, isMagic = used.isMagic,
                forceHit = true, hitTier = HitTier.Reliable
            };
            var res = Context.damage.Compute(info);
            // Stagger build is computed from flat config values and is INDEPENDENT of basePower, so
            // a 35%-power echo would bank a full hit's Break progress (8, or 20 on a weakness, x1.5
            // while the boss telegraphs) — and being forceHit it can never be reduced as a graze
            // either. At a 45% proc on ~3 attacks a round that is over a full extra hit of Break per
            // round, none of which the headless balance gate can see. Pay Break at the same rate the
            // echo pays damage.
            res.staggerBuilt *= followUpPowerFraction;
            Context.damage.Apply(res, Context);
            Context.Log($"    FOLLOW-UP! {actor.displayName} strikes again for {res.amount}.");
            yield return WaitForPresentation();
        }

        // --- announcements ---------------------------------------------------------------
        private void Announce(string line) => onAnnouncement?.Raise(line);

        // "Warrior used Power Strike on The Dragon" — the centre-screen play-by-play, so there is
        // never a "what just happened?" turn.
        private void AnnounceAction(Entity actor, Ability used, Entity[] targets)
        {
            if (actor == null || used == null) return;
            string tgt = "";
            if (used.targetRule == TargetRule.AllEnemies)
                tgt = actor.team == Team.Heroes ? " on all enemies" : " on the party";
            else if (used.targetRule == TargetRule.AllAllies)
                tgt = actor.team == Team.Heroes ? " on the party" : "";
            else if (targets != null && targets.Length > 0 && targets[0] != null && targets[0] != actor)
                tgt = $" on {targets[0].displayName}";
            Announce($"{actor.displayName} used {used.displayName}{tgt}");
        }

        // Single source of truth for affordability (MP + cooldown), used by BOTH the controller (to
        // reject) and the HUD (to grey buttons out) so the two can never drift.
        public static bool CanAfford(Entity hero, Ability a)
            => hero != null && a != null && hero.currentMP >= a.mpCost && !hero.IsOnCooldown(a);

        // Convenience for a "pass/defend with no target" action.
        public void SubmitAction(Ability ability) => SubmitAction(ability, null);

        // The HUD calls this when the active hero uses a consumable: consume one copy from the run
        // inventory FIRST (reject if none owned), then cast the item's wrapped ability through the
        // exact same command pipeline as any skill. The item spends the hero's turn.
        public void SubmitItem(ItemDefinition item, Entity target)
        {
            if (ActiveHero == null || item == null || item.ability == null) return;
            var run = GameBootstrap.Instance?.Run;
            if (run == null || !run.RemoveItem(item.name)) return;
            pendingAction = new ActionRequest(item.ability, ActiveHero, ResolveTargets(ActiveHero, item.ability, target));
        }

        // The HUD calls this when the active hero chooses to reposition (swap front/back row).
        public void SubmitReposition() { if (ActiveHero != null) pendingReposition = true; }

        // --- the player phase: pick who acts -------------------------------------------
        // The HUD calls this from the "choose your hero" panel (or by clicking a hero bar).
        public void SelectHero(Entity hero)
        {
            if (!AwaitingHeroPick || hero == null || !hero.IsAlive || !heroesToAct.Contains(hero)) return;
            pickedHero = hero;
        }

        // Back out of a hero's action menu WITHOUT spending their turn, returning to the pick.
        // Only legal while nothing has been committed — a submitted action is final.
        public void CancelHeroSelection()
        {
            if (ActiveHero != null && pendingAction == null && !pendingReposition) cancelPick = true;
        }

        // The HUD's gold OVERDRIVE button: spend a FULL Valor meter on the party-wide damage surge.
        // A free activation — the hero still takes their action this turn (now surge-boosted), so you
        // "charge up, unleash, then dump a Shatter in the Break window" for the biggest reliable hit.
        public void SubmitOverdrive() => SubmitOverdrive(ChargeSystem.OverdriveMode.Surge);

        // A full Valor meter is a CHOICE, not a button: surge the party's damage, sunder the boss
        // open instantly, or rally the party back from the brink.
        public void SubmitOverdrive(ChargeSystem.OverdriveMode mode)
        {
            if (ActiveHero != null) Context?.charge?.SpendOverdrive(Context, ActiveHero, mode);
        }

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

                // ============ PLAYER PHASE — the player picks who acts, three turns, any order.
                Announce(round == 1 ? "YOUR PHASE — choose a hero" : $"ROUND {round} — YOUR PHASE");

                // Hero damage-over-time resolves once, up front. Ticking at pick-time would let a
                // pick-then-cancel double-tick a burn; resolving it as a phase event makes that
                // impossible by construction (the bug HOLD needed a guard for cannot exist here).
                foreach (var h in Context.heroes)
                {
                    if (h == null || !h.IsAlive) continue;
                    int hdot = h.TickStartOfTurn();
                    if (hdot > 0) Context.Log($"{h.displayName} takes {hdot} damage over time.");
                }
                CheckDeaths();
                Result = Evaluate();
                if (Result != BattleManager.Outcome.InProgress) break;

                heroesToAct.Clear();
                foreach (var h in Context.heroes) if (h != null && h.IsAlive) heroesToAct.Add(h);

                while (heroesToAct.Count > 0 && Result == BattleManager.Outcome.InProgress)
                {
                    heroesToAct.RemoveAll(h => h == null || !h.IsAlive);
                    if (heroesToAct.Count == 0) break;

                    // The pick. When only one hero remains it auto-selects — no pointless click.
                    pickedHero = heroesToAct.Count == 1 ? heroesToAct[0] : null;
                    AwaitingHeroPick = pickedHero == null;
                    while (pickedHero == null && Result == BattleManager.Outcome.InProgress) yield return null;
                    AwaitingHeroPick = false;
                    var actor = pickedHero;
                    pickedHero = null;
                    if (actor == null || !actor.IsAlive) continue;

                    onTurnStarted?.Raise(actor);

                    // Heroes can't currently be control-locked, but if one ever is, it must be a
                    // loud on-screen beat, never a mystery skip.
                    if (!actor.CanAct)
                    {
                        Announce($"{actor.displayName} cannot act!");
                        heroesToAct.Remove(actor);
                        actor.TickEndOfTurn();
                        Context.charge?.ConsumeHeroTurn(Context);
                        onTurnEnded?.Raise(actor);
                        yield return Wait();
                        continue;
                    }

                    // Await the HUD: an ability, an item, a reposition — or backing out to re-pick.
                    ActiveHero = actor;
                    pendingAction = null;
                    pendingReposition = false;
                    cancelPick = false;
                    while (pendingAction == null && !pendingReposition && !cancelPick) yield return null;

                    if (cancelPick)
                    {
                        cancelPick = false;
                        ActiveHero = null;
                        continue;               // nothing spent — back to "choose a hero"
                    }

                    ICommand cmd = null;
                    Ability used = null; Entity[] usedTargets = null;
                    if (pendingReposition)
                    {
                        pendingReposition = false;
                        Reposition(actor);     // spends the turn; cmd stays null so no attack resolves
                        Announce($"{actor.displayName} moves to the {(actor.backRow ? "BACK" : "FRONT")} row");
                        ActiveHero = null;
                        yield return Wait();   // let the move READ before the next turn snaps in
                    }
                    else
                    {
                        var req = pendingAction.Value;
                        used = req.ability; usedTargets = req.targets; cmd = CommandFactory.Build(req);
                        ActiveHero = null;
                        pendingAction = null;
                    }

                    if (cmd != null)
                    {
                        AnnounceAction(actor, used, usedTargets);
                        Context.lastActionResults.Clear();
                        Context.Log(cmd.DescribeForLog());
                        cmd.Resolve(Context);

                        // Non-damaging skills (buffs / heals / stances / defend) don't pass through
                        // OnDamageDealt, so play their cast animation + spawn their VFX here, so
                        // EVERY skill has presentation.
                        if (used != null && used.effectType != EffectType.Attack && used.effectType != EffectType.MultiHit && used.effectType != EffectType.BossMove)
                            PlayNonDamagingFx(actor, used, usedTargets);

                        // Party Valor accrues from this action — coordination (weakness/combo/
                        // setup/buff) charges it hard, a plain spam-hit barely (null-safe).
                        ChargeSystem.AwardFor(used, Context.lastActionResults, Context);

                        yield return WaitForPresentation();

                        // A landed single-target blow may open a FOLLOW-UP window (third action command).
                        if (used != null && usedTargets != null && usedTargets.Length == 1
                            && (used.effectType == EffectType.Attack || used.effectType == EffectType.MultiHit))
                            yield return FollowUpOpportunity(actor, used, usedTargets[0]);
                    }

                    heroesToAct.Remove(actor);
                    actor.TickEndOfTurn();
                    Context.charge?.ConsumeHeroTurn(Context);   // count down an active Overdrive surge
                    onTurnEnded?.Raise(actor);
                    CheckDeaths();
                    Context.boss?.CheckPhaseTransition(Context);
                    Result = Evaluate();
                }
                if (Result != BattleManager.Outcome.InProgress) break;

                // ============ ENEMY PHASE — minions strike first, the boss lands the climax.
                enemyPhaseOrder.Clear();
                foreach (var m in Context.minions) if (m != null && m.IsAlive) enemyPhaseOrder.Add(m);
                if (Context.boss != null && Context.boss.IsAlive) enemyPhaseOrder.Add(Context.boss);
                if (enemyPhaseOrder.Count > 0)
                {
                    Announce("ENEMY PHASE");
                    yield return new WaitForSeconds(0.7f);
                }

                for (int ei = 0; ei < enemyPhaseOrder.Count && Result == BattleManager.Outcome.InProgress; ei++)
                {
                    var actor = enemyPhaseOrder[ei];
                    if (actor == null || !actor.IsAlive) continue;   // fell to a DoT before its turn

                    int dot = actor.TickStartOfTurn();
                    if (dot > 0) Context.Log($"{actor.displayName} takes {dot} damage over time.");
                    onTurnStarted?.Raise(actor);

                    if (actor.CanAct)
                    {
                        ICommand cmd = null;
                        Ability used = null; Entity[] usedTargets = null;
                        Ability ability = null; Entity chosen = null;
                        if (actor.Brain != null)
                            ability = actor.Brain.DecideAction(Context, actor, Context.heroes, out chosen);
                        if (ability != null)
                        {
                            var req = new ActionRequest(ability, actor, ResolveTargets(actor, ability, chosen));
                            used = ability; usedTargets = req.targets;
                            AnnounceAction(actor, used, usedTargets);

                            // BRACE: an enemy is about to land a damaging blow — the timed defensive
                            // action command. The HUD converts a well-timed press into a multiplier.
                            bool damaging = ability.effectType == EffectType.Attack || ability.effectType == EffectType.MultiHit;
                            bool hitsHeroes = false;
                            if (damaging && req.targets != null)
                                foreach (var tt in req.targets) if (tt != null && tt.team == Team.Heroes) { hitsHeroes = true; break; }
                            if (hitsHeroes)
                            {
                                braceLanded = false;
                                braceMult = 1f;
                                braceOpenUntil = Time.time + braceWindow;
                                yield return new WaitForSeconds(braceWindow);
                                if (braceLanded && braceMult < 1f)
                                {
                                    req = new ActionRequest(ability, actor, braceMult, req.targets);
                                    Context.Log($"    BLOCKED! {actor.displayName}'s blow is softened (x{braceMult:0.00}).");
                                }
                            }

                            cmd = CommandFactory.Build(req);
                        }

                        if (cmd != null)
                        {
                            Context.lastActionResults.Clear();
                            Context.Log(cmd.DescribeForLog());
                            cmd.Resolve(Context);
                            if (used != null && used.effectType != EffectType.Attack && used.effectType != EffectType.MultiHit && used.effectType != EffectType.BossMove)
                                PlayNonDamagingFx(actor, used, usedTargets);
                            yield return WaitForPresentation();
                        }
                    }
                    else
                    {
                        // NO SILENT SKIPS: the reason the enemy loses its turn is a full-screen beat
                        // with a visible reel — this is the player's own reward being celebrated.
                        bool frozenSolid = actor.Status != null && actor.Status.HasControlEffect;
                        Announce(frozenSolid
                            ? $"{actor.displayName} is FROZEN SOLID — it cannot act!"
                            : $"{actor.displayName} is BROKEN — it reels helplessly!");
                        Context.Log($"{actor.displayName} is {(frozenSolid ? "frozen" : "staggered")} — it cannot act.");
                        actor.GetComponentInChildren<Characters.AnimationDriver>()?.PlayHit();
                        yield return new WaitForSeconds(1.1f);
                    }

                    actor.TickEndOfTurn();
                    if (actor.isBoss)   // Searing Fury escalates each boss turn; a Break vents it (StaggerSystem)
                    {
                        actor.rageStacks = Mathf.Min(actor.rageStacks + 1, balance.rageMaxStacks);
                        Context.Log($"    {actor.displayName}'s Searing Fury rises to {actor.rageStacks} (+{actor.rageStacks * balance.rageDamagePerStack * 100f:0}% damage — BREAK it to vent!)");
                        // MIRRORED INVARIANT — the same call exists in BattleManager's headless loop.
                        // Change both or the live game and the balance gate drift apart.
                        float breakBefore = actor.staggerMeter;
                        actor.DecayStagger(Context);
                        // DecayStagger only writes to ctx.Log, which is the headless trace — it never
                        // reached the screen. The player watched the gold Break number shrink between
                        // rounds with no explanation, in the one fight whose entire lesson is "you
                        // cannot chip-and-turtle a Break". A rule you cannot see is a rule you can
                        // only lose to.
                        if (actor.staggerMeter < breakBefore - 0.01f)
                            Announce($"{actor.displayName} shakes off your pressure — BREAK −{Mathf.RoundToInt(breakBefore - actor.staggerMeter)}");
                    }
                    onTurnEnded?.Raise(actor);
                    CheckDeaths();
                    Context.boss?.CheckPhaseTransition(Context);
                    Result = Evaluate();
                    yield return new WaitForSeconds(0.45f);   // breathe between enemy turns
                }
            }

            bool won = Result == BattleManager.Outcome.Victory;
            if (won) { Context.Log($"=== VICTORY! {boss.bossName} is slain. ==="); onBattleWon?.Raise(); }
            else { Context.Log("=== DEFEAT. The party has fallen. ==="); onBattleLost?.Raise(); }

            // The RunFlow (presentation) owns the post-battle UX (boon select / run complete /
            // retry), driven by the OnBattleWon / OnBattleLost channels raised above.
        }

        // The action camera, found by interface and cached (the scene has exactly one).
        private IActionCamera actionCamera;
        private bool actionCameraSearched;
        private IActionCamera ActionCamera
        {
            get
            {
                if (!actionCameraSearched)
                {
                    actionCameraSearched = true;
                    foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                        if (mb is IActionCamera ac) { actionCamera = ac; break; }
                }
                return actionCamera;
            }
        }

        // The ability-FX seam (see IAbilityFx), found and cached the same way. Null in a bare or
        // harness scene — which is exactly when PlayNonDamagingFx keeps its old inline feet-bloom.
        private IAbilityFx abilityFx;
        private bool abilityFxSearched;
        private IAbilityFx AbilityFx
        {
            get
            {
                if (!abilityFxSearched)
                {
                    abilityFxSearched = true;
                    foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                        if (mb is IAbilityFx af) { abilityFx = af; break; }
                }
                return abilityFx;
            }
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

            // Roguelite boons (Appendix E.2). Stat deltas are PER HERO; rule changes are per RUN, so
            // ApplyRules is called once per boon and Apply once per boon per hero.
            var mods = new RunModifiers();
            if (run != null && boonRoster != null)
                foreach (var boonName in run.acquiredBoons)
                {
                    var bd = boonRoster.Find(b => b != null && b.name == boonName);
                    if (bd == null) continue;
                    BoonSystem.ApplyRules(mods, bd);
                    foreach (var h in Context.heroes) BoonSystem.Apply(h, bd);
                }
            Context.mods = mods;
            Context.damage.mods = mods;                              // the pipeline reads it for graze stagger
            Context.BossStaggeredTurns += mods.bonusBrokenTurns;     // LINGERING BREAK widens every window
            mods.secondWindUsed = false;                             // a run-long boon, a once-per-FIGHT effect

            // ATTRITION. This used to be a full HP/MP restore, which quietly made most of the meta
            // layer decorative: if every fight starts topped up, the battle grade is a number with no
            // consequence, gold has nothing urgent to buy, and "win with everyone alive" plays exactly
            // like "win with two heroes at 5 HP". Carrying damage forward is what turns the Supply
            // Camp into a real decision — heal now, or gamble the gold on bombs for the next boss.
            //
            // The floors matter as much as the carry: a run must never become mathematically
            // unwinnable because of one bad fight, so nobody starts below half HP however badly the
            // last one went, and a hero who actually fell comes back at exactly the floor.
            foreach (var h in Context.heroes)
            {
                float hpFrac = 1f, mpFrac = 1f;
                if (run != null && run.TryGetCarry(h.displayName, out float ch, out float cm)) { hpFrac = ch; mpFrac = cm; }
                hpFrac = Mathf.Clamp(hpFrac, RunState.CarryHpFloor, 1f);
                mpFrac = Mathf.Clamp(mpFrac, RunState.CarryMpFloor, 1f);
                h.currentHP = Mathf.Max(1, Mathf.RoundToInt(h.stats.maxHP * hpFrac));
                h.currentMP = Mathf.Clamp(Mathf.RoundToInt(h.stats.maxMP * mpFrac), 0, h.stats.maxMP);
            }

            // A clean fast win seeds the next fight's Valor, so the battle grade compounds into the
            // run instead of being a letter on a screen you click past.
            if (run != null && Context.charge != null && run.startValor > 0f)
            {
                Context.charge.valor = Mathf.Min(Context.charge.max, run.startValor);
                Context.Log($"The party marches in with momentum — Valor {Mathf.RoundToInt(Context.charge.valor)}.");
            }

            // Snapshot AFTER the carry is applied: a Retry puts the player back exactly where they
            // walked into this boss, not where they died. Without this, attrition + retry is a death
            // spiral — each attempt starts weaker than the last one that already failed.
            run?.SnapshotForRetry();

            Context.boss = BattleSpawner.SpawnBoss(boss, balance);

            // The boss's adds (live battles only — headless tests never come through here).
            minionDefs.Clear();
            if (boss.minions != null)
                for (int i = 0; i < boss.minions.Count; i++)
                {
                    var md = boss.minions[i];
                    if (md == null) continue;
                    var m = BattleSpawner.SpawnMinion(md, balance, i);
                    minionDefs[m] = md;
                    Context.minions.Add(m);
                }

            ApplyDifficulty(run);
            PlaceCombatants();
        }

        // DIFFICULTY — live battles only, applied to the spawned runtime stats (never the assets).
        // HARD is the baseline the game is balanced around; it also prices in the tools the live
        // player has that the balance-gate AI does not (free hero ordering, no-miss grazes, the
        // timing bar, blocks, follow-ups). EASY softens the enemy side and pads the party.
        private void ApplyDifficulty(RunState run)
        {
            var diff = run != null ? run.difficulty : Difficulty.Hard;
            var enemies = new List<Entity>(Context.minions);
            if (Context.boss != null) enemies.Add(Context.boss);

            // DAMAGE goes through Entity.difficultyDamageMult, NOT stats.baseAttack. Scaling the base
            // stat looked right and did almost nothing: derived Attack is baseAttack + primary*k1
            // (k1=2) and the primary term dominates every enemy block, so "x1.15 ATK" landed as
            // +3.3% on the Dragon and +4.7% on the Evil Warrior — and the Black Mage, whose baseAttack
            // is 0 because he deals MAGIC damage, was completely immune to the setting on both
            // difficulties. One multiplier at the damage step fixes physical and magic uniformly.
            if (diff == Difficulty.Hard)
            {
                // Eased 15% from the first tuning (was 1.15 dmg / 1.10 HP): playtest read as
                // punishing rather than demanding. Hard still means the enemy out-hits and
                // out-lasts its base numbers — it just no longer wipes a competent party.
                foreach (var e in enemies)
                {
                    e.difficultyDamageMult = 0.98f;
                    e.stats.maxHP = Mathf.RoundToInt(e.stats.maxHP * 0.94f);
                    e.currentHP = e.stats.maxHP;
                }
                Context.Log("HARD MODE — the enemy hits harder and endures longer. Combo or die.");
            }
            else
            {
                foreach (var e in enemies)
                {
                    e.difficultyDamageMult = 0.70f;
                    e.stats.maxHP = Mathf.RoundToInt(e.stats.maxHP * 0.85f);
                    e.currentHP = e.stats.maxHP;
                }
                foreach (var h in Context.heroes)
                {
                    int extra = Mathf.RoundToInt(h.stats.maxHP * 0.25f);
                    h.stats.maxHP += extra;
                    h.currentHP = Mathf.Min(h.stats.maxHP, h.currentHP + extra);
                }
                Context.Log("EASY MODE — a gentler arena.");
            }
        }

        // Which definition spawned each minion — read at death time for the item-drop roll.
        private readonly Dictionary<Entity, MinionDefinition> minionDefs = new();

        // Stage the combatants on the orthographic arena: heroes on the left facing the boss
        // on the right. Each gets a placeholder capsule "body" (swapped for real rigged models
        // when those are imported) so the fight is visible. Colours distinguish the classes.
        private static readonly Color[] HeroPalette =
        {
            new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.9f),
            new Color(0.6f, 0.35f, 0.8f), new Color(0.35f, 0.75f, 0.4f)
        };

        // Battle-line formation. The fight axis is deliberately LATERAL (left-to-right across the
        // lens) rather than running away into the screen: heroes face the boss, so if the boss sits
        // much deeper in Z than they do, "face the boss" becomes "turn your back to the player".
        // Keeping every combatant within ~1.5u of the same depth means facing the enemy reads as a
        // clean profile, which the camera-blend then rotates into a flattering 3/4 front view.
        // The gentle stagger still separates the three heroes in frame and adds depth.
        private static Vector3 HeroSlot(bool backRow, int indexInRow)
            => backRow ? new Vector3(-3.85f - indexInRow * 1.45f, 0f, 0.15f + indexInRow * 0.95f)
                       : new Vector3(-2.05f - indexInRow * 1.25f, 0f, -1.25f + indexInRow * 0.85f);

        // Re-place every hero into tidy row slots (party order preserved). Used at battle start and
        // again after a mid-battle row swap so the wedge never ends up with holes.
        private void LayoutHeroes(bool animate)
        {
            int front = 0, back = 0;
            foreach (var h in Context.heroes)
            {
                var slot = HeroSlot(h.backRow, h.backRow ? back++ : front++);
                h.transform.position = slot;   // the logical anchor (HUD/targeting) moves immediately
                var motion = h.GetComponent<CombatantMotion>();
                if (motion == null) continue;
                if (animate) motion.SlideBase(slot);   // the BODY jogs across
                else motion.MoveBase(slot);
            }
        }

        private void PlaceCombatants()
        {
            // Right of frame at roughly the party's depth, so the heroes turn to PROFILE (not away)
            // to face it. Pushed out again (5.1 -> 7.4) because a 30%-larger dragon plus a four-whelp
            // skirmish line had the two armies practically touching — the arena needs visible
            // no-man's-land between the lines for the charge to read as a charge.
            var bossPos = new Vector3(7.4f, 0f, -0.25f);
            int frontIdx = 0, backIdx = 0;
            for (int i = 0; i < Context.heroes.Count; i++)
            {
                var h = Context.heroes[i];
                h.backRow = h.primaryStat != PrimaryStat.STR;                          // STR melee = front line; casters/ranged = back
                h.transform.position = HeroSlot(h.backRow, h.backRow ? backIdx++ : frontIdx++);
                h.transform.rotation = Quaternion.Euler(0, 90, 0);
                Vector3 faceBoss = bossPos - h.transform.position; faceBoss.y = 0f;
                float scale = h.modelPrefab != null ? 1.2f : 1f;     // make the 3D heroes read larger
                // camBlend 0.75: the heroes stand at ~90 degrees to the lens when they face the boss,
                // so each 0.01 of blend is worth ~0.9 degrees of yaw toward the camera. 0.48 still
                // showed too much shoulder; 0.75 turns them a further ~25 degrees into a proper
                // three-quarter view. Kept below 1.0 so they are never facing the player outright,
                // which would read as ignoring the enemy they are about to hit.
                AttachBody(h.gameObject, h.modelPrefab, h.stageSprite, HeroPalette[i % HeroPalette.Length], 1f, 1.9f, i, faceBoss, scale, 0f, 0.75f);
                var motion = h.gameObject.AddComponent<CombatantMotion>();    // lunge/recoil (+ procedural bob if no model)
                if (h.modelPrefab != null) motion.bobAmplitude = 0f;          // the Animator's Idle replaces the bob
            }
            if (Context.boss != null)
            {
                Context.boss.transform.position = bossPos;
                Context.boss.transform.rotation = Quaternion.Euler(0, -90, 0);
                Vector3 faceHeroes = (Context.heroes.Count > 0 ? Context.heroes[0].transform.position : Vector3.zero) - bossPos; faceHeroes.y = 0f;
                // The boss is BIG but still parses at a glance next to ~2.5u heroes. Height is
                // per-boss data: the dragon looms at 5u, humanoid bosses read right around 3.2-3.6.
                // Mostly face the heroes (low camera blend) so attacks visibly aim at the party.
                AttachBody(Context.boss.gameObject, Context.boss.modelPrefab, Context.boss.stageSprite, new Color(0.5f, 0.12f, 0.12f), 2.0f, 3.0f, 0, faceHeroes, 1f, Mathf.Max(2f, boss.modelHeight), 0.18f);
                var bm = Context.boss.gameObject.AddComponent<CombatantMotion>();
                bm.lungeDistance = 0.8f;
                bm.bobAmplitude = Context.boss.modelPrefab != null ? 0f : 0.12f;   // a heavier-feeling 2D boss bobs
            }

            // Minions form a skirmish line in front of their master, staggered so they never
            // block the boss silhouette from the camera.
            for (int i = 0; i < Context.minions.Count; i++)
            {
                var m = Context.minions[i];
                minionDefs.TryGetValue(m, out var md);
                // adds form a staggered skirmish LINE between the armies (two alternating depths) —
                // they read as the threat you must clear first. Held back toward their master now
                // that the boss sits at 7.4, so the whelps screen the dragon instead of crowding
                // the party's front rank.
                var mp = new Vector3(3.15f + i * 1.15f, 0f, -1.65f - (i % 2) * 0.95f);
                m.transform.position = mp;
                Vector3 faceParty = new Vector3(-4.4f, 0f, 0f) - mp; faceParty.y = 0f;
                float mh = md != null ? md.modelHeight : 2.2f;
                AttachBody(m.gameObject, m.modelPrefab, m.stageSprite, new Color(0.45f, 0.2f, 0.2f), 1.2f, 2.0f, 0, faceParty, 1f, mh, 0.22f);
                if (md != null && md.animatorOverride != null)
                {
                    var anim = m.GetComponentInChildren<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = md.animatorOverride;
                }
                var mm = m.gameObject.AddComponent<CombatantMotion>();
                mm.lungeDistance = 0.6f;
                mm.bobAmplitude = 0f;
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
            foreach (var m in Context.minions) if (m != null) AddShadow(m.gameObject, 1.7f);   // adds were floating shadowless
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
            // Turn toward whoever this is for before casting — buffing an ally while facing the
            // opposite way was one of the "unnatural" reads.
            if (targets != null && targets.Length > 0 && targets[0] != null && targets[0] != caster)
            {
                Vector3 look = targets[0].transform.position - caster.transform.position; look.y = 0f;
                caster.GetComponent<CombatantMotion>()?.FaceTarget(look);
            }
            caster.GetComponentInChildren<RPGArena.Characters.AnimationDriver>()?.PlayCast();
            // Heals and buffs never reach OnDamageDealt, so they would be the one action type with
            // no action-camera punch-in. Driven through IActionCamera so this assembly never names
            // the presentation layer (the IBattleIntro pattern).
            ActionCamera?.FocusOnActor(caster);

            // Hand the whole effect to presentation when the seam is present. Only the UI layer can
            // reach the renderer-measured anchors, the boss-scale multiplier, ProjectileFlight and
            // the code-built elemental burst — this assembly must never name them (UI -> Gameplay is
            // one-way). The seam returns how long it needs, so a THROWN bomb gets to land before the
            // turn advances; WaitForPresentation (called right after us) honours PresentationBusyUntil
            // and hard-caps itself at 2.5s, so a bad estimate can never hang the fight.
            var fxSeam = AbilityFx;
            if (fxSeam != null)
            {
                float need = fxSeam.PlayAbilityFx(caster, ability, targets);
                if (need > 0f)
                    PresentationBusyUntil = Mathf.Max(PresentationBusyUntil, Time.time + need + 0.35f);
                return;
            }

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
                // Every animated body needs a driver. Hero prefabs author one; the vendor boss/minion
                // prefabs (BlackMageWizard, the Nightmare whelps) do NOT — without this they never
                // play a single attack/hit/death clip and just stand there frozen all fight.
                if (model.GetComponentInChildren<Characters.AnimationDriver>() == null)
                    model.AddComponent<Characters.AnimationDriver>();
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
            var opponents = actor.team == Team.Heroes ? Context.Enemies : Context.heroes;
            switch (ability.targetRule)
            {
                case TargetRule.AllEnemies: return TargetingSystem.AllAlive(opponents).ToArray();
                case TargetRule.AllAllies:
                    var allies = actor.team == Team.Heroes ? Context.heroes : Context.Enemies;
                    return TargetingSystem.AllAlive(allies).ToArray();
                case TargetRule.Self: return new[] { actor };
                // MIRRORED INVARIANT — BattleManager.ResolveTargets clamps identically.
                case TargetRule.SingleAlly: return new[] { TargetingSystem.ClampToAlly(actor, single) };
                default:
                    // Adds-first: while the boss has living minions, a hero's untargeted single-enemy
                    // attack strikes the skirmish line before the boss (classic clear-the-adds phase).
                    var t = TargetingSystem.ClampToEnemy(actor, single);
                    if (t == null && actor.team == Team.Heroes)
                        foreach (var m in Context.minions) if (m != null && m.IsAlive) { t = m; break; }
                    if (t == null) t = TargetingSystem.FirstAlive(opponents);
                    return t != null ? new[] { t } : new Entity[0];
            }
        }

        // Swap a hero between the front and back row mid-battle (the positioning move). The whole
        // wedge re-forms around the change so the formation never ends up with holes.
        private void Reposition(Entity hero)
        {
            if (hero == null) return;
            hero.backRow = !hero.backRow;
            LayoutHeroes(true);
            Context.Log($"{hero.displayName} repositions to the {(hero.backRow ? "back" : "front")} row.");
        }

        private List<Entity> All()
        {
            var all = new List<Entity>(Context.heroes);
            if (Context.boss != null) all.Add(Context.boss);
            foreach (var m in Context.minions) if (m != null) all.Add(m);
            return all;
        }

        private readonly HashSet<Entity> announced = new();
        private void CheckDeaths()
        {
            foreach (var e in All())
            {
                if (e.IsAlive) continue;
                // MIRRORED INVARIANT — the same guard runs in BattleManager.CheckDeaths.
                if (BoonSystem.TrySecondWind(e, Context)) continue;
                if (announced.Add(e))
                {
                    Context.Log($"{e.displayName} has fallen.");
                    // Minion drops: gold always, the authored item on a lucky roll.
                    if (e.team == Team.Enemies && !e.isBoss)
                    {
                        var run = GameBootstrap.Instance?.Run;
                        if (run != null && minionDefs.TryGetValue(e, out var md))
                        {
                            if (md.goldDrop > 0)
                            {
                                run.gold += md.goldDrop;
                                Context.Log($"    +{md.goldDrop} gold  (total {run.gold})");
                            }
                            if (md.itemDrop != null && Context.rng.NextDouble() < md.itemDropChance)
                            {
                                run.AddItem(md.itemDrop.name);
                                Context.Log($"    {md.itemDrop.displayName} dropped!");
                            }
                        }
                    }
                    onEntityDied?.Raise(e);
                }
            }
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
