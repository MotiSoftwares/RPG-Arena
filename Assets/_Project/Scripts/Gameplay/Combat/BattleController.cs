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
        public Entity ActiveHero { get; private set; }                // whose input we await (null otherwise)
        public bool AwaitingInput => ActiveHero != null && pendingAction == null;
        public BattleManager.Outcome Result { get; private set; } = BattleManager.Outcome.InProgress;

        private ActionRequest? pendingAction;

        private void Start() => StartCoroutine(RunBattle());

        // The HUD calls this when the active hero chooses an ability + target.
        public void SubmitAction(Ability ability, Entity target)
        {
            if (ActiveHero == null || ability == null) return;
            pendingAction = new ActionRequest(ability, ActiveHero, ResolveTargets(ActiveHero, ability, target));
        }

        // Convenience for a "pass/defend with no target" action.
        public void SubmitAction(Ability ability) => SubmitAction(ability, null);

        private IEnumerator RunBattle()
        {
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
                    Context.Log("You studied the Dragon: weak to ICE, absorbs FIRE.");
                }
            }

            Context.Log($"=== {boss.bossName} appears! ===");
            onBattleStarted?.Raise();
            yield return Wait();

            for (int round = 1; round <= 60 && Result == BattleManager.Outcome.InProgress; round++)
            {
                RoundsTaken = round;
                var order = Context.turns.BuildRoundOrder(All(), Context.rng, balance.maxExtraTurnsPerEntityPerRound);
                while (order.Count > 0 && Result == BattleManager.Outcome.InProgress)
                {
                    var actor = order.Dequeue();
                    if (actor == null || !actor.IsAlive) continue;

                    int dot = actor.TickStartOfTurn();
                    if (dot > 0) Context.Log($"{actor.displayName} takes {dot} damage over time.");
                    onTurnStarted?.Raise(actor);

                    if (actor.CanAct)
                    {
                        ICommand cmd = null;

                        if (actor.Brain != null)
                        {
                            // AI-controlled (the boss).
                            var opponents = actor.team == Team.Heroes ? new List<Entity> { Context.boss } : Context.heroes;
                            var ability = actor.Brain.DecideAction(Context, actor, opponents, out var tgt);
                            if (ability != null)
                                cmd = CommandFactory.Build(new ActionRequest(ability, actor, ResolveTargets(actor, ability, tgt)));
                        }
                        else
                        {
                            // Player hero: open the action menu and wait for the HUD to submit.
                            ActiveHero = actor;
                            pendingAction = null;
                            while (pendingAction == null) yield return null;
                            cmd = CommandFactory.Build(pendingAction.Value);
                            ActiveHero = null;
                            pendingAction = null;
                        }

                        if (cmd != null)
                        {
                            Context.lastActionResults.Clear();
                            Context.Log(cmd.DescribeForLog());
                            cmd.Resolve(Context);

                            // Action economy (§5.5): a weakness hit or crit grants the HERO one
                            // capped bonus turn (the boss never earns invisible extra turns).
                            if (actor.team == Team.Heroes)
                                foreach (var r in Context.lastActionResults)
                                    if (r.hit && (r.reaction == ElementReaction.Weak || r.crit))
                                    {
                                        if (Context.turns.TryGrantExtraTurn(actor, order))
                                            Context.Log($"    +1 MORE! {actor.displayName} seizes another action.");
                                        break;
                                    }

                            yield return Wait();
                        }
                    }
                    else
                    {
                        Context.Log($"{actor.displayName} is frozen/staggered — turn skipped.");
                        yield return Wait();
                    }

                    actor.TickEndOfTurn();
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
                BossStaggeredTurns = Mathf.Max(2, boss.staggeredTurns),
                damage = new DamagePipeline(balance, new System.Random()),
                stagger = new StaggerSystem(),
                turns = new TurnSystem(),
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
            for (int i = 0; i < Context.heroes.Count; i++)
            {
                var h = Context.heroes[i];
                h.transform.position = new Vector3(-5f + i * 1.7f, 0f, i * 0.4f);
                h.transform.rotation = Quaternion.Euler(0, 90, 0);
                AttachBody(h.gameObject, h.stageSprite, HeroPalette[i % HeroPalette.Length], 1f, 1.9f, i);
                h.gameObject.AddComponent<CombatantMotion>();          // procedural idle/lunge/recoil
            }
            if (Context.boss != null)
            {
                Context.boss.transform.position = new Vector3(4.5f, 0f, 0.6f);
                Context.boss.transform.rotation = Quaternion.Euler(0, -90, 0);
                AttachBody(Context.boss.gameObject, Context.boss.stageSprite, new Color(0.5f, 0.12f, 0.12f), 2.3f, 3.4f, 0);
                var bm = Context.boss.gameObject.AddComponent<CombatantMotion>();
                bm.bobAmplitude = 0.12f; bm.lungeDistance = 0.8f;     // a heavier-feeling boss
            }

            DressStage();
        }

        // Turns the bare stage into a place: a full-screen arena backdrop behind everyone and a
        // soft contact shadow under each combatant so they don't float (the 2.5D look, §10).
        private void DressStage()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (boss != null && boss.arenaBackdrop != null && GameObject.Find("Backdrop") == null)
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

            for (int i = 0; i < Context.heroes.Count; i++) AddShadow(Context.heroes[i].gameObject, 1.2f);
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
            sr.color = new Color(0f, 0f, 0f, 0.5f);
            sr.sortingOrder = -1;
            go.transform.localRotation = Quaternion.Inverse(host.transform.rotation);   // face camera
            float sx = width / 0.64f;                       // shadow sprite is 64px @ 100ppu = 0.64u
            go.transform.localScale = new Vector3(sx, sx * 0.32f, 1f);
            go.transform.localPosition = new Vector3(0f, 0.14f, 0.02f);
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
        private static void AttachBody(GameObject host, Sprite sprite, Color color, float width, float height, int order)
        {
            if (host.transform.Find("Body") != null) return;

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
    }
}
